using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Acme.DomainAnalyzers;

/// <summary>
/// Flags domain types that are a plain <c>class</c>/<c>struct</c> (R6): a domain type must be a
/// <c>record</c> (record class), a <c>record struct</c>, an <c>interface</c>, or an <c>enum</c>.
/// Two necessary exemptions apply because such types cannot be records: a <c>static</c> class
/// (utility holder) and a class deriving from a non-record base other than <c>object</c> (it must
/// remain a class to extend a framework base, e.g. an <c>Exception</c>- or <c>JsonConverter&lt;T&gt;</c>-derived
/// type). Only fires when the compilation opts in via the MSBuild property
/// <c>EnforceImmutability=true</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DomainTypeKindAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = $"{DiagnosticIds.Prefix}002";

    private const string EnforcePropertyKey = "build_property.EnforceImmutability";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Domain types must be a record, record struct, interface, or enum",
        messageFormat: "Domain type '{0}' must be a record, record struct, interface, or enum (not a {1})",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Domain types must be a record (record class), a record struct, an interface, or "
            + "an enum. A plain class/struct is not allowed, except a static class or a class deriving "
            + "from a non-record base other than object (which cannot be a record)."
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

        // Skip compiler-generated types.
        if (type.IsImplicitlyDeclared)
        {
            return;
        }

        // Only plain class/struct are candidates. interface/enum/delegate are not classes/structs;
        // a record (class) or record struct satisfies the rule.
        if (type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct)
        {
            return;
        }

        if (type.IsRecord)
        {
            return;
        }

        // Exemption: a static class is a utility holder and cannot be a record.
        if (type.IsStatic)
        {
            return;
        }

        // Exemption: a class with a base class other than object that is not itself a record must
        // remain a class to extend a framework base (e.g. Exception, JsonConverter<T>). This applies
        // to classes only — a struct's base is always ValueType, which is not an extensible base.
        if (
            type.TypeKind == TypeKind.Class
            && type.BaseType is { } baseType
            && baseType.SpecialType != SpecialType.System_Object
            && !baseType.IsRecord
        )
        {
            return;
        }

        var kind = type.TypeKind == TypeKind.Struct ? "struct" : "class";
        var location = type.Locations.Length > 0 ? type.Locations[0] : Location.None;
        context.ReportDiagnostic(Diagnostic.Create(Rule, location, type.Name, kind));
    }

    private static bool IsEnforcementEnabled(AnalyzerOptions options)
    {
        var globalOptions = options.AnalyzerConfigOptionsProvider.GlobalOptions;
        return globalOptions.TryGetValue(EnforcePropertyKey, out var value)
            && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}
