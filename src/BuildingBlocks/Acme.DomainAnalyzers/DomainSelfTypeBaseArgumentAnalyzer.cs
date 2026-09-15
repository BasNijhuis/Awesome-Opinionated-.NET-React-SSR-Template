using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Acme.DomainAnalyzers;

/// <summary>
/// Flags a type whose base type (or directly implemented interface) is a constructed generic type with
/// a <c>TSelf</c> type parameter to which it passes something other than itself. The canonical case is
/// <c>AggregateRoot&lt;TSelf, TId&gt;</c>, whose <c>RaiseEvent</c> casts the <c>with</c>-clone to
/// <c>TSelf</c>: <c>record Gadget : AggregateRoot&lt;Widget, WidgetId&gt;</c> compiles — the
/// <c>where TSelf : AggregateRoot&lt;TSelf, TId&gt;</c> constraint cannot express "must be the declaring
/// type" — but throws <see cref="InvalidCastException"/> on the first raise. This is the declaration-site
/// counterpart of <c>ACME004</c> (which guards the same convention on generic <em>method</em> calls).
/// Only fires when the compilation opts in via the MSBuild property <c>EnforceImmutability=true</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DomainSelfTypeBaseArgumentAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = $"{DiagnosticIds.Prefix}009";

    private const string SelfTypeParameterName = "TSelf";

    private const string EnforcePropertyKey = "build_property.EnforceImmutability";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Self-type argument must be the declaring type",
        messageFormat: "'{0}' has '{1}' as the 'TSelf' argument of '{2}'; a self-typed base must be "
            + "closed with the declaring type",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A generic base type or interface with a 'TSelf' type parameter (e.g. "
            + "AggregateRoot<TSelf, TId>) casts to TSelf and compares identities by it at runtime. The "
            + "declaring type must be that argument; the generic constraint cannot express this, so "
            + "another type compiles and then throws InvalidCastException or collapses two aggregates "
            + "onto one identity. This includes inheriting a closed self-typed base through an "
            + "intermediate type: the intermediate must forward TSelf (be generic in it) instead of "
            + "closing it."
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
        if (!IsEnforcementEnabled(context.Options))
        {
            return;
        }

        var type = (INamedTypeSymbol)context.Symbol;

        // The whole base chain, not just the direct base: an intermediate type that *closes* TSelf
        // (`abstract record Audited : AggregateRoot<Audited, WidgetId>`) is itself valid, but every
        // leaf under it then inherits TSelf = Audited — so two different leaves sharing an id compare
        // as the same aggregate (TrackedAggregates would replace one with the other and silently drop
        // its pending events) and RaiseEvent degrades to returning the intermediate. The leaf is where
        // that manifests, so that is where it is reported. One diagnostic per type is enough.
        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (AnalyzeBase(context, type, baseType))
            {
                break;
            }
        }

        // Interfaces: only the ones this type declares. An interface inherited through a base class or
        // another interface is that type's contract to get right, not this one's.
        foreach (var directInterface in type.Interfaces)
        {
            AnalyzeBase(context, type, directInterface);
        }
    }

    /// <summary>Checks one base type/interface; returns true when it reported a diagnostic.</summary>
    private static bool AnalyzeBase(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        INamedTypeSymbol baseType
    )
    {
        if (!baseType.IsGenericType)
        {
            return false;
        }

        // Find the type parameter named "TSelf" (the self-type convention).
        var typeParameters = baseType.OriginalDefinition.TypeParameters;
        var selfIndex = -1;
        for (var i = 0; i < typeParameters.Length; i++)
        {
            if (
                string.Equals(
                    typeParameters[i].Name,
                    SelfTypeParameterName,
                    StringComparison.Ordinal
                )
            )
            {
                selfIndex = i;
                break;
            }
        }

        // Not a self-typed base — ignore.
        if (selfIndex < 0 || selfIndex >= baseType.TypeArguments.Length)
        {
            return false;
        }

        var typeArgument = baseType.TypeArguments[selfIndex];

        // Skip unresolved/error types (incomplete code) — don't pile on diagnostics.
        if (typeArgument.TypeKind == TypeKind.Error)
        {
            return false;
        }

        // Skip a forwarded type parameter (e.g. an abstract middle layer that passes its own TSelf
        // through to the base); it cannot be compared to the concrete declaring type.
        if (typeArgument.TypeKind == TypeKind.TypeParameter)
        {
            return false;
        }

        if (SymbolEqualityComparer.Default.Equals(typeArgument, type))
        {
            return false;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Rule,
                type.Locations.Length > 0 ? type.Locations[0] : Location.None,
                type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                typeArgument.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                baseType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
            )
        );

        return true;
    }

    private static bool IsEnforcementEnabled(AnalyzerOptions options)
    {
        var globalOptions = options.AnalyzerConfigOptionsProvider.GlobalOptions;
        return globalOptions.TryGetValue(EnforcePropertyKey, out var value)
            && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}
