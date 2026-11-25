using System.Diagnostics;
using System.Runtime.CompilerServices;
using Acme.DomainAbstractions;
using Acme.TestSupport;
using AwesomeAssertions.Execution;

namespace Acme.DomainAbstractions.Tests;

public sealed class ResultAssertionsTests
{
    [Fact]
    public void ShouldSucceed_on_a_failure_fails_with_the_errors()
    {
        // Arrange
        Result<int> result = Error.NotFound("missing", "Nope.");

        // Act
        var act = () => result.ShouldSucceed();

        // Assert
        act.Should().Throw<Exception>().WithMessage("*NotFound/missing: Nope.*");
    }

    [Fact]
    public void ShouldFail_on_a_success_fails_with_the_value()
    {
        // Arrange
        Result<int> result = 42;

        // Act
        var act = () => result.ShouldFail();

        // Assert
        act.Should().Throw<Exception>().WithMessage("*42*");
    }

    [Fact]
    public void A_default_result_fails_the_assertion_instead_of_crashing_the_switch()
    {
        // Arrange
        var result = default(Result<int>);

        // Act
        var act = () => result.ShouldSucceed();

        // Assert
        act.Should()
            .Throw<Exception>()
            .WithMessage("*neither*")
            .Which.Should()
            .NotBeOfType<SwitchExpressionException>();
    }

    [Fact]
    public void Inside_an_assertion_scope_the_failure_is_still_reported()
    {
        // Arrange
        Result<int> result = Error.Conflict("taken", "Already there.");

        // Act: the helper must return a value, so it fails immediately even inside a scope.
        var act = () =>
        {
            using var scope = new AssertionScope();
            return result.ShouldSucceed();
        };

        // Assert
        act.Should()
            .Throw<Exception>()
            .WithMessage("*Conflict/taken*")
            .Which.Should()
            .NotBeOfType<UnreachableException>();
    }
}
