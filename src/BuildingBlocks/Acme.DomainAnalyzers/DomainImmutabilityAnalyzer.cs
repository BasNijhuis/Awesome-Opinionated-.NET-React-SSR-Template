using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Acme.DomainAnalyzers;

/// <summary>
/// Flags mutable state in domain types (R5): properties with a <c>set</c> accessor (R5a, any
/// accessibility), non-<c>readonly</c> instance fields (R5b, any accessibility), and non-<c>private</c>
/// members typed as a mutable collection (R5c — only the exposed surface; a <c>private</c> field is not
/// exposed mutable state). Only fires when the compilation opts in via the MSBuild property
/// <c>EnforceImmutability=true</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DomainImmutabilityAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = $"{DiagnosticIds.Prefix}001";

    private const string EnforcePropertyKey = "build_property.EnforceImmutability";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Domain types must be immutable",
        messageFormat: "Domain types must be immutable: '{0}' exposes mutable state ({1})",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Domain types must be immutable: state must use 'init'/get-only properties and "
            + "'readonly' fields, and exposed collections must use read-only shapes. A property with a "
            + "'set' accessor, a non-readonly instance field, or a non-private member typed as a mutable "
            + "collection violates this."
    );

    // Simple (unqualified) names of mutable collection types that are not allowed.
    private static readonly ImmutableHashSet<string> MutableCollectionTypeNames =
        ImmutableHashSet.Create(
            "List",
            "HashSet",
            "Dictionary",
            "SortedSet",
            "SortedDictionary",
            "Queue",
            "Stack",
            "ICollection",
            "IList",
            "ISet",
            "IDictionary"
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
            switch (member)
            {
                case IPropertySymbol property:
                    AnalyzeProperty(context, property);
                    break;
                case IFieldSymbol field:
                    AnalyzeField(context, field);
                    break;
            }
        }
    }

    private static void AnalyzeProperty(SymbolAnalysisContext context, IPropertySymbol property)
    {
        if (property.IsImplicitlyDeclared)
        {
            return;
        }

        // (R5a) Any property with a 'set' accessor — incl. private set. 'init' and get-only are allowed.
        if (property.SetMethod is { IsInitOnly: false })
        {
            Report(context, property, "has a 'set' accessor");
            return;
        }

        // (R5c) A non-private member typed as a mutable collection. A private member is not exposed
        // mutable state, so it is allowed (e.g. the AggregateRoot domain-events outbox).
        if (!IsPrivate(property) && IsMutableCollectionType(property.Type))
        {
            Report(context, property, "is typed as a mutable collection");
        }
    }

    private static void AnalyzeField(SymbolAnalysisContext context, IFieldSymbol field)
    {
        if (field.IsImplicitlyDeclared)
        {
            // Backing fields for auto-properties etc. are handled via the property.
            return;
        }

        // const and static fields are fine (const is implicitly static; static state is not
        // instance state). A non-readonly instance field is mutable state.
        if (field.IsConst || field.IsStatic)
        {
            return;
        }

        // (R5b) A non-readonly instance field (any accessibility).
        if (!field.IsReadOnly)
        {
            Report(context, field, "is a non-readonly field");
            return;
        }

        // (R5c) A non-private readonly field typed as a mutable collection is exposed mutable state.
        // A private field (e.g. the AggregateRoot domain-events outbox) is allowed.
        if (!IsPrivate(field) && IsMutableCollectionType(field.Type))
        {
            Report(context, field, "is typed as a mutable collection");
        }
    }

    private static bool IsMutableCollectionType(ITypeSymbol type)
    {
        // Arrays (T[]) are always mutable.
        if (type.TypeKind == TypeKind.Array)
        {
            return true;
        }

        if (type is INamedTypeSymbol named)
        {
            var simpleName = named.Name;
            if (MutableCollectionTypeNames.Contains(simpleName))
            {
                return true;
            }
        }

        return false;
    }

    // A private member is not part of the type's exposed surface, so a mutable-collection type on it
    // (R5c) is not "exposed mutable state". Anything more visible (internal/protected/public) is.
    private static bool IsPrivate(ISymbol symbol) =>
        symbol.DeclaredAccessibility == Accessibility.Private;

    private static bool IsEnforcementEnabled(AnalyzerOptions options, ISymbol symbol)
    {
        var globalOptions = options.AnalyzerConfigOptionsProvider.GlobalOptions;
        if (
            globalOptions.TryGetValue(EnforcePropertyKey, out var value)
            && string.Equals(value, "true", System.StringComparison.OrdinalIgnoreCase)
        )
        {
            return true;
        }

        return false;
    }

    private static void Report(SymbolAnalysisContext context, ISymbol member, string reason)
    {
        var location = member.Locations.Length > 0 ? member.Locations[0] : Location.None;
        context.ReportDiagnostic(Diagnostic.Create(Rule, location, member.Name, reason));
    }
}
