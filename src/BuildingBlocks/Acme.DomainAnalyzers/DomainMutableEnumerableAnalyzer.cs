using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Acme.DomainAnalyzers;

/// <summary>
/// Flags a <strong>mutable collection type</strong> appearing in the declared signature or members of a
/// domain type (R5c, broadened): a field type, property type, method/constructor parameter type, or
/// method return type. Mutable types are the writable BCL collections (<c>List&lt;&gt;</c>,
/// <c>IList&lt;&gt;</c>, <c>ICollection&lt;&gt;</c>, <c>HashSet&lt;&gt;</c>, <c>ISet&lt;&gt;</c>,
/// <c>Dictionary&lt;&gt;</c>, <c>IDictionary&lt;&gt;</c>, <c>SortedSet&lt;&gt;</c>,
/// <c>SortedDictionary&lt;&gt;</c>, <c>Queue&lt;&gt;</c>, <c>Stack&lt;&gt;</c>) and array types
/// (<c>T[]</c>). The read-only shapes (<c>IReadOnlyList&lt;&gt;</c>, <c>IReadOnlyCollection&lt;&gt;</c>,
/// <c>IReadOnlyDictionary&lt;&gt;</c>, <c>IReadOnlySet&lt;&gt;</c>, <c>IEnumerable&lt;&gt;</c>) and
/// <c>System.Collections.Immutable.*</c> are allowed.
/// <para>
/// Scope is declared types in signatures + members only — method-body locals and expressions are not
/// analyzed, so a transient <c>.ToList()</c>/<c>[.. …]</c> that materializes into a read-only shape is
/// fine. Only fires when the compilation opts in via the MSBuild property
/// <c>EnforceImmutability=true</c>.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DomainMutableEnumerableAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = $"{DiagnosticIds.Prefix}003";

    private const string EnforcePropertyKey = "build_property.EnforceImmutability";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Domain types must not use mutable enumerable types",
        messageFormat: "Domain types must not use mutable collections: '{0}' uses '{1}' (use a read-only or immutable shape)",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Domain types must not expose or accept mutable collection types in their fields, "
            + "properties, parameters, or return types. Use IReadOnlyList<>/IReadOnlyCollection<>/"
            + "IReadOnlyDictionary<>/IReadOnlySet<>/IEnumerable<> or System.Collections.Immutable instead "
            + "of List<>, arrays, HashSet<>, Dictionary<>, Queue<>, Stack<>, and their mutable interfaces."
    );

    // Fully-qualified metadata names of the mutable collection types that are not allowed. Matching by
    // metadata name (not simple name) avoids false positives on same-named user types.
    private static readonly ImmutableHashSet<string> MutableCollectionMetadataNames =
        ImmutableHashSet.Create(
            "System.Collections.Generic.List`1",
            "System.Collections.Generic.IList`1",
            "System.Collections.Generic.ICollection`1",
            "System.Collections.Generic.HashSet`1",
            "System.Collections.Generic.ISet`1",
            "System.Collections.Generic.Dictionary`2",
            "System.Collections.Generic.IDictionary`2",
            "System.Collections.Generic.SortedSet`1",
            "System.Collections.Generic.SortedDictionary`2",
            "System.Collections.Generic.Queue`1",
            "System.Collections.Generic.Stack`1"
        );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
        context.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
    }

    private static void AnalyzeField(SymbolAnalysisContext context)
    {
        if (!IsEnforcementEnabled(context.Options))
        {
            return;
        }

        var field = (IFieldSymbol)context.Symbol;
        if (field.IsImplicitlyDeclared)
        {
            return;
        }

        ReportIfMutable(context, field.Type, field);
    }

    private static void AnalyzeProperty(SymbolAnalysisContext context)
    {
        if (!IsEnforcementEnabled(context.Options))
        {
            return;
        }

        var property = (IPropertySymbol)context.Symbol;
        if (property.IsImplicitlyDeclared)
        {
            return;
        }

        ReportIfMutable(context, property.Type, property);
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        if (!IsEnforcementEnabled(context.Options))
        {
            return;
        }

        var method = (IMethodSymbol)context.Symbol;
        if (method.IsImplicitlyDeclared)
        {
            return;
        }

        // Skip accessors (property/event get/set/add/remove) — the associated property/event is
        // analyzed directly, so reporting on the accessor would double-flag the same declared type.
        if (method.AssociatedSymbol is not null)
        {
            return;
        }

        switch (method.MethodKind)
        {
            case MethodKind.PropertyGet:
            case MethodKind.PropertySet:
            case MethodKind.EventAdd:
            case MethodKind.EventRemove:
                return;
        }

        // Return type (constructors and void methods have no meaningful return-type concern here).
        if (method.MethodKind != MethodKind.Constructor && !method.ReturnsVoid)
        {
            ReportIfMutable(context, method.ReturnType, method);
        }

        foreach (var parameter in method.Parameters)
        {
            ReportIfMutable(context, parameter.Type, parameter);
        }
    }

    private static void ReportIfMutable(
        SymbolAnalysisContext context,
        ITypeSymbol type,
        ISymbol declaringSymbol
    )
    {
        if (!IsMutableCollectionType(type))
        {
            return;
        }

        var location =
            declaringSymbol.Locations.Length > 0 ? declaringSymbol.Locations[0] : Location.None;
        context.ReportDiagnostic(
            Diagnostic.Create(
                Rule,
                location,
                declaringSymbol.Name,
                type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
            )
        );
    }

    private static bool IsMutableCollectionType(ITypeSymbol type)
    {
        // Array types (T[]) are always mutable.
        if (type.TypeKind == TypeKind.Array)
        {
            return true;
        }

        if (type is INamedTypeSymbol named && named.IsGenericType)
        {
            // e.g. "System.Collections.Generic" + "." + "List`1" -> "System.Collections.Generic.List`1".
            // MetadataName carries the `n arity suffix; ContainingNamespace gives the namespace.
            var definition = named.OriginalDefinition;
            var ns = definition.ContainingNamespace?.ToDisplayString();
            var fullyQualified = string.IsNullOrEmpty(ns)
                ? definition.MetadataName
                : $"{ns}.{definition.MetadataName}";
            if (MutableCollectionMetadataNames.Contains(fullyQualified))
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
