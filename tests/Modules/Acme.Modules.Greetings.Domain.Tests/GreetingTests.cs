using Acme.Modules.Greetings.Domain.Contracts;

namespace Acme.Modules.Greetings.Domain.Tests;

public sealed class GreetingTests
{
    [Fact]
    public void Create_sets_a_trimmed_message_a_fresh_id_and_a_timestamp()
    {
        // Act
        var greeting = Greeting.Create(new CreateGreetingSpec("  hello  "));

        // Assert
        greeting.Message.Should().Be("hello");
        greeting.Id.Value.Should().NotBe(Guid.Empty);
        greeting.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_rejects_a_blank_message()
    {
        // Act
        var act = () => Greeting.Create(new CreateGreetingSpec("   "));

        // Assert
        act.Should().Throw<ArgumentException>();
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
