using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Acme.DomainAnalyzers;

/// <summary>
/// Flags a read of <c>Result&lt;T&gt;.Value</c> that is not guarded by a success check on the same
/// result. <c>Value</c> throws on a failed result (ADR-0013), so it may only be read on a path where
/// <c>IsSuccess</c> is known true.
/// <para>
/// Recognised guards, all keyed to the <em>same</em> result symbol (local, parameter, field or
/// property) the <c>Value</c> is read from:
/// <list type="bullet">
///   <item>an early-exit guard earlier in the block — <c>if (r.IsFailure) return;</c> /
///   <c>if (!r.IsSuccess) throw …;</c> (the body must return/throw/break/continue);</item>
///   <item>the success branch of a condition — inside <c>if (r.IsSuccess) { … }</c>, the
///   <c>else</c> of <c>if (r.IsFailure) … else { … }</c>, or the true arm of
///   <c>r.IsSuccess ? r.Value : …</c>.</item>
/// </list>
/// Reading <c>Value</c> off a temporary (e.g. <c>Create(spec).Value</c>) is always flagged: an
/// inline result cannot have been checked. Compound conditions (<c>r.IsSuccess &amp;&amp; …</c>) and
/// guards spread across non-lexical control flow are intentionally not recognised — capture, check,
/// then read. Opt-in via <c>EnforceResultValueGuard=true</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ResultValueGuardAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = $"{DiagnosticIds.Prefix}007";

    private const string EnforcePropertyKey = "build_property.EnforceResultValueGuard";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Result<T>.Value must be guarded by a success check",
        messageFormat: "Read 'Value' only after confirming the result succeeded (guard with 'if (result.IsFailure) return;' or read it inside an 'if (result.IsSuccess)' branch)",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Result<T>.Value throws on a failed result (ADR-0013). Read it only where IsSuccess "
            + "is known true: after an early-return/throw guard on IsFailure, or inside an "
            + "if (IsSuccess) / ternary success branch on the same result."
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);
    }

    private static void AnalyzePropertyReference(OperationAnalysisContext context)
    {
        if (!IsEnforcementEnabled(context.Options))
        {
            return;
        }

        var propertyRef = (IPropertyReferenceOperation)context.Operation;
        if (
            propertyRef.Instance is not { } instance
            || !IsResultValueProperty(propertyRef.Property)
        )
        {
            return;
        }

        // A temporary receiver (Create(spec).Value, Result.Success(x).Value, …) has no symbol to have
        // checked, so it can never be guarded — report it. A stable symbol may be guarded.
        var receiver = GetReferencedSymbol(instance);
        if (receiver is not null && IsGuarded(propertyRef, receiver))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, propertyRef.Syntax.GetLocation()));
    }

    // True when `operation` reads Result<T>.Value defined in a `*.DomainAbstractions` namespace/assembly
    // (the building block; rename-safe). Other `.Value` members (Lazy<T>, Nullable<T>, strongly-typed
    // ids) are left alone.
    private static bool IsResultValueProperty(IPropertySymbol property)
    {
        if (property.Name != "Value")
        {
            return false;
        }

        var containing = property.ContainingType;
        return containing is { Name: "Result", IsGenericType: true }
            && IsDomainAbstractions(containing);
    }

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

    private static ISymbol? GetReferencedSymbol(IOperation operation) =>
        operation switch
        {
            ILocalReferenceOperation local => local.Local,
            IParameterReferenceOperation parameter => parameter.Parameter,
            IFieldReferenceOperation field => field.Field,
            IPropertyReferenceOperation property => property.Property,
            _ => null,
        };

    private static bool IsGuarded(IOperation valueAccess, ISymbol receiver)
    {
        for (var op = valueAccess.Parent; op is not null; op = op.Parent)
        {
            switch (op)
            {
                case IConditionalOperation conditional:
                    // Inside `if (r.IsSuccess) { … }` / `r.IsSuccess ? … : …` (or the else of a
                    // failure check) the branch we sit in guarantees success.
                    if (
                        conditional.WhenTrue is { } whenTrue
                        && Contains(whenTrue, valueAccess)
                        && BranchImpliesSuccess(
                            conditional.Condition,
                            receiver,
                            conditionWasTrue: true
                        )
                    )
                    {
                        return true;
                    }

                    if (
                        conditional.WhenFalse is { } whenFalse
                        && Contains(whenFalse, valueAccess)
                        && BranchImpliesSuccess(
                            conditional.Condition,
                            receiver,
                            conditionWasTrue: false
                        )
                    )
                    {
                        return true;
                    }

                    break;

                case IBlockOperation block:
                    if (HasEarlyExitGuard(block, valueAccess, receiver))
                    {
                        return true;
                    }

                    break;
            }
        }

        return false;
    }

    // A preceding `if (<failure>) <exit>;` (no else) in the same block guarantees the result succeeded
    // for every statement after it.
    private static bool HasEarlyExitGuard(
        IBlockOperation block,
        IOperation valueAccess,
        ISymbol receiver
    )
    {
        foreach (var statement in block.Operations)
        {
            if (Contains(statement, valueAccess))
            {
                // Reached the access without finding a guard before it in this block.
                return false;
            }

            if (
                statement is IConditionalOperation { WhenFalse: null } guard
                && TryGetSuccessCheck(guard.Condition, receiver, out var conditionTrueMeansSuccess)
                && !conditionTrueMeansSuccess
                && AlwaysExits(guard.WhenTrue)
            )
            {
                return true;
            }
        }

        return false;
    }

    private static bool BranchImpliesSuccess(
        IOperation condition,
        ISymbol receiver,
        bool conditionWasTrue
    ) =>
        TryGetSuccessCheck(condition, receiver, out var conditionTrueMeansSuccess)
        && (conditionWasTrue ? conditionTrueMeansSuccess : !conditionTrueMeansSuccess);

    // Recognises `r.IsSuccess`, `r.IsFailure` and any `!` chain over them, for the given receiver.
    // `conditionTrueMeansSuccess` says whether the condition being *true* proves success.
    private static bool TryGetSuccessCheck(
        IOperation condition,
        ISymbol receiver,
        out bool conditionTrueMeansSuccess
    )
    {
        conditionTrueMeansSuccess = false;

        var negated = false;
        while (condition is IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } unary)
        {
            negated = !negated;
            condition = unary.Operand;
        }

        if (
            condition is not IPropertyReferenceOperation property
            || property.Instance is not { } instance
            || !SymbolEqual(GetReferencedSymbol(instance), receiver)
        )
        {
            return false;
        }

        bool baseMeansSuccess;
        switch (property.Property.Name)
        {
            case "IsSuccess":
                baseMeansSuccess = true;
                break;
            case "IsFailure":
                baseMeansSuccess = false;
                break;
            default:
                return false;
        }

        conditionTrueMeansSuccess = negated ? !baseMeansSuccess : baseMeansSuccess;
        return true;
    }

    // Control never falls through to the next statement: return / throw / break / continue / goto, or
    // a block whose last statement does the same.
    private static bool AlwaysExits(IOperation? operation) =>
        operation switch
        {
            IReturnOperation => true,
            IThrowOperation => true,
            IBranchOperation => true,
            IExpressionStatementOperation expression => AlwaysExits(expression.Operation),
            IBlockOperation block => block.Operations.Length > 0
                && AlwaysExits(block.Operations[block.Operations.Length - 1]),
            _ => false,
        };

    private static bool Contains(IOperation container, IOperation node)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, container))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SymbolEqual(ISymbol? left, ISymbol? right) =>
        left is not null && SymbolEqualityComparer.Default.Equals(left, right);

    private static bool IsEnforcementEnabled(AnalyzerOptions options)
    {
        var globalOptions = options.AnalyzerConfigOptionsProvider.GlobalOptions;
        return globalOptions.TryGetValue(EnforcePropertyKey, out var value)
            && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}
