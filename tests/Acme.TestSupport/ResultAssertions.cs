using System.Diagnostics;
using Acme.DomainAbstractions;
using AwesomeAssertions;

namespace Acme.TestSupport;

/// <summary>
/// Asserts which case a <see cref="Result{T}"/> holds and hands back its contents, replacing the
/// <c>IsSuccess</c> + <c>Value</c> pattern of the pre-union result (ADR-0020).
/// </summary>
public static class ResultAssertions
{
    private const string NeitherCase =
        "found default(Result<T>), which holds neither a success nor a failure";

    /// <summary>Asserts the result is a success and returns its value.</summary>
    public static T ShouldSucceed<T>(this Result<T> result) =>
        result switch
        {
            Success<T>(var value) => value,
            Failure failure => Fail<T>(
                $"Expected a successful result, but found a failure with {Describe(failure)}."
            ),
            _ => Fail<T>($"Expected a successful result, but {NeitherCase}."),
        };

    /// <summary>Asserts the result is a failure and returns it, for assertions on its errors.</summary>
    public static Failure ShouldFail<T>(this Result<T> result) =>
        result switch
        {
            Failure failure => failure,
            Success<T>(var value) => Fail<Failure>(
                $"Expected a failed result, but found a success with {value}."
            ),
            _ => Fail<Failure>($"Expected a failed result, but {NeitherCase}."),
        };

    private static string Describe(Failure failure) =>
        string.Join("; ", failure.Errors.Select(e => $"{e.Category}/{e.Code}: {e.Message}"));

    // Throws the test framework's assertion exception directly, even inside an AssertionScope: the
    // helpers must hand back a value, so they can't just record the failure and carry on.
    private static TReturn Fail<TReturn>(string message)
    {
        AssertionEngine.TestFramework.Throw(message);
        throw new UnreachableException();
    }
}
