using System.Diagnostics;
using Acme.DomainAbstractions;
using AwesomeAssertions.Execution;

namespace Acme.TestSupport;

/// <summary>
/// Asserts which case a <see cref="Result{T}"/> holds and hands back its contents, replacing the
/// <c>IsSuccess</c> + <c>Value</c> pattern of the pre-union result (ADR-0020).
/// </summary>
public static class ResultAssertions
{
    /// <summary>Asserts the result is a success and returns its value.</summary>
    public static T ShouldSucceed<T>(this Result<T> result) =>
        result switch
        {
            Success<T>(var value) => value,
            Failure failure => Fail<T>(
                "Expected a successful result, but found a failure with {0}.",
                Describe(failure)
            ),
        };

    /// <summary>Asserts the result is a failure and returns it, for assertions on its errors.</summary>
    public static Failure ShouldFail<T>(this Result<T> result) =>
        result switch
        {
            Failure failure => failure,
            Success<T>(var value) => Fail<Failure>(
                "Expected a failed result, but found a success with {0}.",
                value
            ),
        };

    private static string Describe(Failure failure) =>
        string.Join("; ", failure.Errors.Select(e => $"{e.Category}/{e.Code}: {e.Message}"));

    private static TReturn Fail<TReturn>(string message, object? argument)
    {
        AssertionChain.GetOrCreate().FailWith(message, argument);
        throw new UnreachableException("FailWith throws outside an assertion scope.");
    }
}
