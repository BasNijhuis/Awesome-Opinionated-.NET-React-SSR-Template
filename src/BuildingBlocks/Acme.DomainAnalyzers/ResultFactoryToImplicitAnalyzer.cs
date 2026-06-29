using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Acme.DomainAnalyzers;

/// <summary>
/// Flags a <c>Result.Success(…)</c> / <c>Result&lt;T&gt;.Success(…)</c> / <c>Result.Failure(…)</c>
/// call in a <c>return</c> position where the implicit <c>Result&lt;T&gt;</c> conversion would do —
/// i.e. <c>return value;</c> / <c>return error;</c> reads cleaner than wrapping the same value in a
/// factory call (ADR-0013).
/// <para>
/// Only the convertible shapes are flagged: any <c>Success</c> (a single value), and <c>Failure</c>
/// taking a single <see cref="Error"/> or an <c>ImmutableArray&lt;Error&gt;</c>. The
/// <c>Failure(IReadOnlyList&lt;Error&gt;)</c> overload is left alone — C# has no implicit conversion
/// from an interface (CS0552), so propagating an existing error list must call it explicitly. Calls
/// outside a <c>return</c> (e.g. an argument to <c>Task.FromResult(…)</c>, where generic inference
/// would bind the wrong type), calls whose return target is the non-generic <see cref="Result"/>, and
/// calls inside <see cref="Result"/>'s own members (the conversions/factories themselves) are not
/// flagged. Opt-in via <c>EnforceResultImplicitConversion=true</c>.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ResultFactoryToImplicitAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = $"{DiagnosticIds.Prefix}008";

    private const string EnforcePropertyKey = "build_property.EnforceResultImplicitConversion";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Prefer the implicit Result<T> conversion over Result.Success/Result.Failure",
        messageFormat: "Return the {0} directly and let it convert to Result<T> instead of calling Result.{1}(…)",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Result<T> has implicit conversions from a value, an Error, and an "
            + "ImmutableArray<Error> (ADR-0013), so a returned value/error needs no Success/Failure "
            + "wrapper. The Failure(IReadOnlyList<Error>) overload stays (no implicit conversion from an "
            + "interface)."
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

        // Calls inside Result's own members (its conversion operators and Success/Failure helpers call
        // each other — there's no implicit form to use there).
        if (IsResultType(context.ContainingSymbol?.ContainingType))
        {
            return;
        }

        if (!IsResultType(method.ContainingType) || !TryGetFactoryKind(method, out var kind))
        {
            return;
        }

        // The return target must be a generic Result<T> (so the implicit conversion applies) — not the
        // non-generic Result, which has no value/error conversion.
        if (
            context.ContainingSymbol is not IMethodSymbol container
            || !IsGenericResult(UnwrapAsync(container.ReturnType))
        )
        {
            return;
        }

        // ...and the call must be the returned expression (possibly through a cast or a ternary) — not,
        // say, an argument to Task.FromResult(…), where generic inference would bind the wrong type.
        if (!IsInReturnPosition(invocation))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Rule,
                invocation.Syntax.GetLocation(),
                kind == "Success" ? "value" : "error",
                kind
            )
        );
    }

    // "Success" for any Success(value); "Failure" only for the Error / ImmutableArray<Error> overloads.
    private static bool TryGetFactoryKind(IMethodSymbol method, out string kind)
    {
        kind = method.Name;
        if (method.Name == "Success")
        {
            return true;
        }

        if (method.Name == "Failure" && method.Parameters.Length == 1)
        {
            var parameterType = method.Parameters[0].Type;
            return IsErrorType(parameterType) || IsImmutableArrayOfError(parameterType);
        }

        return false;
    }

    // The call is the returned expression, reached through any implicit conversion or ternary wrapping.
    private static bool IsInReturnPosition(IOperation operation)
    {
        var node = operation;
        for (
            var parent = node.Parent;
            parent is IConversionOperation or IConditionalOperation;
            parent = parent.Parent
        )
        {
            node = parent;
        }

        return node.Parent is IReturnOperation;
    }

    // The inner result type of Task<…>/ValueTask<…> (an async method's `return` targets it), else the
    // type itself.
    private static ITypeSymbol UnwrapAsync(ITypeSymbol type) =>
        type is INamedTypeSymbol { IsGenericType: true, Name: "Task" or "ValueTask" } named
        && named.TypeArguments.Length == 1
            ? named.TypeArguments[0]
            : type;

    private static bool IsResultType(ITypeSymbol? type) =>
        type is INamedTypeSymbol { Name: "Result" } named && IsDomainAbstractions(named);

    private static bool IsGenericResult(ITypeSymbol? type) =>
        type is INamedTypeSymbol { Name: "Result", IsGenericType: true } named
        && IsDomainAbstractions(named);

    private static bool IsErrorType(ITypeSymbol type) =>
        type is INamedTypeSymbol { Name: "Error" } named && IsDomainAbstractions(named);

    private static bool IsImmutableArrayOfError(ITypeSymbol type) =>
        type is INamedTypeSymbol { Name: "ImmutableArray", IsGenericType: true } named
        && named.TypeArguments.Length == 1
        && IsErrorType(named.TypeArguments[0]);

    private static bool IsDomainAbstractions(INamedTypeSymbol type)
    {
        var assembly = type.ContainingAssembly?.Name;
        if (
            assembly is not null
            && assembly.EndsWith(".DomainAbstractions", StringComparison.Ordinal)
        )
        {
            return true;
        }

        for (
            var ns = type.ContainingNamespace;
            ns is { IsGlobalNamespace: false };
            ns = ns.ContainingNamespace
        )
        {
            if (ns.Name == "DomainAbstractions")
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
