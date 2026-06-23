using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Acme.DomainAnalyzers;

/// <summary>
/// Flags a "self-type" generic-method invocation whose <c>TSelf</c> type argument is not the type
/// that contains the call site. The canonical case is <c>AggregateRoot.RaiseEvent&lt;TSelf&gt;</c>,
/// which casts <c>this</c> to <c>TSelf</c>: writing <c>next.RaiseEvent&lt;GameRound&gt;(evt)</c> inside
/// <c>Session</c> compiles but throws <see cref="InvalidCastException"/> at runtime. Any generic method
/// with a type parameter named <c>TSelf</c> must be called with the enclosing type as that argument.
/// Only fires when the compilation opts in via the MSBuild property
/// <c>EnforceImmutability=true</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DomainSelfTypeArgumentAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = $"{DiagnosticIds.Prefix}004";

    private const string SelfTypeParameterName = "TSelf";

    private const string EnforcePropertyKey = "build_property.EnforceImmutability";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Self-type argument must be the calling type",
        messageFormat: "The 'TSelf' type argument must be the calling type '{0}', but was '{1}'",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A generic method with a 'TSelf' type parameter (e.g. AggregateRoot.RaiseEvent) "
            + "casts the receiver to TSelf at runtime. The TSelf type argument must therefore be the "
            + "type that contains the call site; supplying a different type throws InvalidCastException."
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        if (!IsEnforcementEnabled(context.Options))
        {
            return;
        }

        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        // Only generic methods can carry a self-type parameter.
        if (!method.IsGenericMethod)
        {
            return;
        }

        // Find the type parameter named "TSelf" (the self-type convention).
        var typeParameters = method.TypeParameters;
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

        // Not a self-type method — ignore.
        if (selfIndex < 0)
        {
            return;
        }

        var typeArgument = method.TypeArguments[selfIndex];

        // Skip unresolved/error types (incomplete code) — don't pile on diagnostics.
        if (typeArgument.TypeKind == TypeKind.Error)
        {
            return;
        }

        // Skip a forwarded type parameter (e.g. a generic helper that itself passes TSelf through);
        // it cannot be compared to a concrete enclosing type.
        if (typeArgument.TypeKind == TypeKind.TypeParameter)
        {
            return;
        }

        // The enclosing type = nearest INamedTypeSymbol up from the call site's containing symbol.
        var enclosingType = GetEnclosingNamedType(context.ContainingSymbol);
        if (enclosingType is null)
        {
            return;
        }

        if (!SymbolEqualityComparer.Default.Equals(typeArgument, enclosingType))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Rule,
                    invocation.Syntax.GetLocation(),
                    enclosingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    typeArgument.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                )
            );
        }
    }

    private static INamedTypeSymbol? GetEnclosingNamedType(ISymbol? symbol)
    {
        for (var current = symbol; current is not null; current = current.ContainingSymbol)
        {
            if (current is INamedTypeSymbol named)
            {
                return named;
            }
        }

        return null;
    }

    private static bool IsEnforcementEnabled(AnalyzerOptions options)
    {
        var globalOptions = options.AnalyzerConfigOptionsProvider.GlobalOptions;
        return globalOptions.TryGetValue(EnforcePropertyKey, out var value)
            && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}
