using System.Collections.Immutable;
using Acme.DomainAbstractions;

namespace Acme.DomainAbstractions.Tests;

public sealed class ResultTests
{
    [Fact]
    public void Success_is_successful_and_has_no_error()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Failure_carries_the_error()
    {
        var error = Error.Conflict("session_full", "The tavern is full.");

        var result = Result.Failure(error);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
        result.Errors.Should().ContainSingle().Which.Should().Be(error);
    }

    [Fact]
    public void Generic_success_exposes_the_value()
    {
        var result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Accessing_value_of_a_failure_throws()
    {
        var result = Result<int>.Failure(Error.NotFound("missing", "Nope."));

        result.Invoking(r => r.Value).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Accessing_error_of_a_success_throws()
    {
        var result = Result.Success();

        result.Invoking(r => r.Error).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Validation_failure_keeps_all_field_errors()
    {
        ImmutableArray<Error> errors =
        [
            Error.Validation("hostName", "Required."),
            Error.Validation("persona", "Invalid."),
        ];

        var result = Result<string>.Failure(errors);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().OnlyContain(e => e.Category == ErrorCategory.Validation);
    }
}
