using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Acme.DomainAbstractions;
using Acme.TestSupport;

namespace Acme.DomainAbstractions.Tests;

public sealed class ResultTests
{
    private static readonly Error Missing = Error.NotFound("missing", "Nope.");

    [Fact]
    public void A_value_converts_to_the_success_case()
    {
        // Act
        Result<int> result = 42;

        // Assert
        result.ShouldSucceed().Should().Be(42);
    }

    [Fact]
    public void An_error_converts_to_a_failure_with_that_error()
    {
        // Act
        Result<int> result = Missing;

        // Assert
        var failure = result.ShouldFail();
        failure.Error.Should().Be(Missing);
        failure.Errors.Should().ContainSingle().Which.Should().Be(Missing);
    }

    [Fact]
    public void Errors_convert_to_a_failure_with_all_of_them()
    {
        // Arrange
        ImmutableArray<Error> errors =
        [
            Error.Validation("Name", "Name is required."),
            Error.Validation("Quantity", "Quantity must not be negative."),
        ];

        // Act
        Result<string> result = errors;

        // Assert
        var failure = result.ShouldFail();
        failure.Errors.Should().Equal(errors);
        failure.Error.Should().Be(errors[0]);
    }

    [Fact]
    public void A_failure_without_errors_throws()
    {
        // Act
        var act = () =>
        {
            Result<int> result = ImmutableArray<Error>.Empty;
            return result;
        };

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_failure_from_a_default_array_throws()
    {
        // Act
        var act = () =>
        {
            Result<int> result = default(ImmutableArray<Error>);
            return result;
        };

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_switch_expression_over_both_cases_needs_no_discard()
    {
        // Arrange
        Result<int> result = 21;

        // Act: compiles only because the two cases are exhaustive (CS8509 is an error, ADR-0020).
        var doubled = result switch
        {
            Success<int>(var value) => value * 2,
            Failure failure => -failure.Errors.Length,
        };

        // Assert
        doubled.Should().Be(42);
    }

    [Fact]
    public void A_default_result_matches_neither_case()
    {
        // Act
        var result = default(Result<int>);

        // Assert: why code that may only continue after a success matches Success<T>, never `is not Failure`.
        (result is Success<int>).Should().BeFalse();
        (result is Failure).Should().BeFalse();
    }

    [Fact]
    public void Result_is_a_union()
    {
        typeof(Result<int>).Should().Implement<IUnion>();
    }
}
