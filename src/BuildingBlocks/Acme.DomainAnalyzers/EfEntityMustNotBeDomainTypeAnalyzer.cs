using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Acme.DomainAnalyzers;

/// <summary>
/// Flags a **domain type** (any type in a <c>*.Domain</c>/<c>*.DomainAbstractions</c> assembly or
/// namespace) used as an **EF entity / relationship / owned-type registration** — as a generic type
/// argument or a <c>typeof(T)</c> argument to a <c>Microsoft.EntityFrameworkCore</c> API
/// (<c>DbSet&lt;T&gt;</c>, <c>Set&lt;T&gt;</c>, <c>Entity</c>, <c>HasOne</c>, <c>HasMany</c>,
/// <c>OwnsOne</c>, <c>OwnsMany</c>, <c>ComplexProperty</c>, <c>Navigation</c>). The persistence layer
/// must map Application-layer <c>*Entity</c> POCOs, never the domain aggregates/value objects
/// (ADR-0016/0018). Value-object <c>Property(…)/HasConversion(…)</c> mappings are intentionally NOT
/// flagged. Only fires when the compilation opts in via <c>EnforceEfEntityBoundary=true</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EfEntityMustNotBeDomainTypeAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = $"{DiagnosticIds.Prefix}006";

    private const string EnforcePropertyKey = "build_property.EnforceEfEntityBoundary";
    private const string EfNamespacePrefix = "Microsoft.EntityFrameworkCore";

    // EF APIs that register an entity / related entity / owned or complex type (so their type
    // argument is an *entity-ish* type). Deliberately excludes Property/HasConversion, whose generic
    // argument is a value-object property type (the sanctioned mapping).
    private static readonly ImmutableHashSet<string> RegistrationMethods = ImmutableHashSet.Create(
        "Entity",
        "Set",
        "HasOne",
        "HasMany",
        "OwnsOne",
        "OwnsMany",
        "ComplexProperty",
        "Navigation"
    );

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Domain types must not be mapped as EF entities",
        messageFormat: "Domain type '{0}' must not be mapped as an EF entity; map an Application-layer persistence entity (e.g. '{0}Entity') instead",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "EF must map Application-layer persistence entities, never domain aggregates or "
            + "value objects (ADR-0016/0018). Use the module's '*Entity' POCO instead. Value-object "
            + "Property(...)/HasConversion(...) mappings are exempt."
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        context.RegisterSymbolAction(AnalyzeMember, SymbolKind.Property, SymbolKind.Field);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        if (!IsEnforcementEnabled(context.Options))
        {
            return;
        }

        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        if (!IsEfRegistrationMethod(method))
        {
            return;
        }

        foreach (var typeArgument in method.TypeArguments)
        {
            if (IsDomainType(typeArgument))
            {
                Report(context, invocation.Syntax.GetLocation(), typeArgument);
            }
        }

        // `Entity(typeof(GameRound))` and similar Type-argument overloads.
        foreach (var argument in invocation.Arguments)
        {
            if (argument.Value is ITypeOfOperation typeOf && IsDomainType(typeOf.TypeOperand))
            {
                Report(context, argument.Value.Syntax.GetLocation(), typeOf.TypeOperand);
            }
        }
    }

    private static void AnalyzeMember(SymbolAnalysisContext context)
    {
        if (!IsEnforcementEnabled(context.Options))
        {
            return;
        }

        var memberType = context.Symbol switch
        {
            IPropertySymbol property => property.Type,
            IFieldSymbol field => field.Type,
            _ => null,
        };

        if (
            memberType is INamedTypeSymbol { TypeArguments.Length: 1 } named
            && named.Name == "DbSet"
            && IsInEfNamespace(named)
            && IsDomainType(named.TypeArguments[0])
        )
        {
            var location =
                context.Symbol.Locations.Length > 0 ? context.Symbol.Locations[0] : Location.None;
            Report(context.ReportDiagnostic, location, named.TypeArguments[0]);
        }
    }

    private static void Report(
        OperationAnalysisContext context,
        Location location,
        ITypeSymbol type
    ) => Report(context.ReportDiagnostic, location, type);

    private static void Report(Action<Diagnostic> report, Location location, ITypeSymbol type) =>
        report(Diagnostic.Create(Rule, location, type.Name));

    private static bool IsEfRegistrationMethod(IMethodSymbol method) =>
        RegistrationMethods.Contains(method.Name) && IsInEfNamespace(method.ContainingType);

    private static bool IsInEfNamespace(INamedTypeSymbol? type)
    {
        var ns = type?.ContainingNamespace?.ToDisplayString();
        return ns is not null
            && (
                ns == EfNamespacePrefix
                || ns.StartsWith(EfNamespacePrefix + ".", StringComparison.Ordinal)
            );
    }

    // A domain type = any type in a `*.Domain`/`*.DomainAbstractions` assembly, or whose namespace has
    // a `Domain`/`DomainAbstractions` segment (catches Kernel.Domain.Sessions value objects too).
    private static bool IsDomainType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named || named.TypeKind == TypeKind.Error)
        {
            return false;
        }

        var assembly = named.ContainingAssembly?.Name;
        if (
            assembly is not null
            && (
                assembly.EndsWith(".Domain", StringComparison.Ordinal)
                || assembly.EndsWith(".DomainAbstractions", StringComparison.Ordinal)
            )
        )
        {
            return true;
        }

        for (
            var ns = named.ContainingNamespace;
            ns is { IsGlobalNamespace: false };
            ns = ns.ContainingNamespace
        )
        {
            if (ns.Name is "Domain" or "DomainAbstractions")
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEnforcementEnabled(AnalyzerOptions options)
    {
        var globalOptions = options.AnalyzerConfigOptionsProvider.GlobalOptions;
        return globalOptions.TryGetValue(EnforcePropertyKey, out var value)
            && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}
