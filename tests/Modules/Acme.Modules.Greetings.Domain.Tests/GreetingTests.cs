using Acme.DomainAbstractions;
using Acme.Modules.Greetings.Domain.Contracts;

namespace Acme.Modules.Greetings.Domain.Tests;

public sealed class GreetingTests
{
    [Fact]
    public void Create_sets_a_trimmed_message_a_fresh_id_and_a_timestamp()
    {
        // Act
        var result = Greeting.Create(new CreateGreetingSpec("  hello  "));

        // Assert
        result.IsSuccess.Should().BeTrue();
        var greeting = result.Value;
        greeting.Message.Should().Be("hello");
        greeting.Id.Value.Should().NotBe(Guid.Empty);
        greeting.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_rejects_a_blank_message()
    {
        // Act
        var result = Greeting.Create(new CreateGreetingSpec("   "));

        // Assert — an expected, recoverable failure (Validation), not an exception
        result.IsFailure.Should().BeTrue();
        result.Error.Category.Should().Be(ErrorCategory.Validation);
        result.Error.Code.Should().Be("Message");
    }

    [Fact]
    public void Rehydrate_round_trips_state()
    {
        // Arrange
        var state = new GreetingState(GreetingId.New(), "hi", DateTimeOffset.UtcNow);

        // Act
        var greeting = Greeting.Rehydrate(state);

        // Assert
        greeting.Id.Should().Be(state.Id);
        greeting.Message.Should().Be(state.Message);
        greeting.CreatedAt.Should().Be(state.CreatedAt);
    }

    private sealed record GreetingState(GreetingId Id, string Message, DateTimeOffset CreatedAt)
        : IGreetingState;
}
