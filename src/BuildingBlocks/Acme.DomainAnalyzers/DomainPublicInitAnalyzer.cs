using System;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Acme.DomainAnalyzers;

/// <summary>
/// Flags <b>public</b> <c>init</c> accessors in domain types (#36). Domain state is set only inside the
/// declaring type — via a constructor/factory and internal <c>with</c> expressions — so an explicitly
/// declared <c>public init</c> (which lets callers outside the aggregate set state through an object
/// initializer or <c>with</c>, bypassing invariants) is forbidden. <c>private</c>/<c>protected</c>/
/// <c>internal</c> init, get-only, and <c>readonly</c> are allowed. Compiler-generated accessors
/// (positional-record primary-constructor parameters) are exempt — their primary constructor is the
/// sanctioned construction path. Only fires when the compilation opts in via the MSBuild property
/// <c>EnforceImmutability=true</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DomainPublicInitAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = $"{DiagnosticIds.Prefix}005";

    private const string EnforcePropertyKey = "build_property.EnforceImmutability";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Domain types must not expose public init accessors",
        messageFormat: "Domain type '{0}' exposes a public 'init' accessor on '{1}'; use a non-public "
            + "init (construct via constructor/factory)",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Domain state is set only inside the declaring type (constructor/factory plus "
            + "internal 'with'). An explicitly declared 'public init' lets callers outside the type set "
            + "state through an object initializer or 'with', bypassing invariants. Use a non-public init "
            + "('private'/'protected'/'internal'). Compiler-generated accessors (positional-record "
            + "parameters) are exempt."
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        if (!IsEnforcementEnabled(context.Options, context.Symbol))
        {
            return;
        }

        var type = (INamedTypeSymbol)context.Symbol;

        // Do not analyze enums; only analyze concrete state-bearing types.
        if (type.TypeKind == TypeKind.Enum)
        {
            return;
        }

        foreach (var member in type.GetMembers())
        {
            if (member is IPropertySymbol property)
            {
                AnalyzeProperty(context, property);
            }
        }
    }

    private static void AnalyzeProperty(SymbolAnalysisContext context, IPropertySymbol property)
    {
        // Exempt compiler-synthesized members and positional-record parameters: the primary constructor
        // is the sanctioned construction path. A positional-record property is *not* IsImplicitlyDeclared
        // (its synthesized property maps back to the parameter), so detect the parameter syntax too.
        if (
            property.IsImplicitlyDeclared
            || IsPositionalRecordParameter(property, context.CancellationToken)
        )
        {
            return;
        }

        // An explicitly declared, public 'init' accessor. private/protected/internal init are allowed.
        if (property.SetMethod is { IsInitOnly: true, DeclaredAccessibility: Accessibility.Public })
        {
            Report(context, property);
        }
    }

    // A positional record's property is generated from a primary-constructor parameter; its declaring
    // syntax is the ParameterSyntax (an explicitly written property's is a PropertyDeclarationSyntax).
    private static bool IsPositionalRecordParameter(
        IPropertySymbol property,
        CancellationToken cancellationToken
    )
    {
        foreach (var reference in property.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(cancellationToken) is ParameterSyntax)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEnforcementEnabled(AnalyzerOptions options, ISymbol symbol)
    {
        var globalOptions = options.AnalyzerConfigOptionsProvider.GlobalOptions;
        if (
            globalOptions.TryGetValue(EnforcePropertyKey, out var value)
            && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
        )
        {
            return true;
        }

        return false;
    }

    private static void Report(SymbolAnalysisContext context, IPropertySymbol property)
    {
        var location = property.Locations.Length > 0 ? property.Locations[0] : Location.None;
        context.ReportDiagnostic(
            Diagnostic.Create(Rule, location, property.ContainingType.Name, property.Name)
        );
    }
}
