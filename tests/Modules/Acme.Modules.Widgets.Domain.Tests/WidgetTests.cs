using Acme.Modules.Widgets.Domain.Contracts;

namespace Acme.Modules.Widgets.Domain.Tests;

public sealed class WidgetTests
{
    [Fact]
    public void Create_sets_a_trimmed_name_quantity_a_fresh_id_and_a_timestamp()
    {
        // Act
        var widget = Widget.Create(new CreateWidgetSpec("  Gadget  ", 7));

        // Assert
        widget.Name.Should().Be("Gadget");
        widget.Quantity.Should().Be(7);
        widget.Id.Value.Should().NotBe(Guid.Empty);
        widget.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_rejects_a_blank_name()
    {
        // Act
        var act = () => Widget.Create(new CreateWidgetSpec("   ", 1));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rehydrate_round_trips_state()
    {
        // Arrange
        var state = new WidgetState(WidgetId.New(), "Sprocket", 3, DateTimeOffset.UtcNow);

        // Act
        var widget = Widget.Rehydrate(state);

        // Assert
        widget.Id.Should().Be(state.Id);
        widget.Name.Should().Be(state.Name);
        widget.Quantity.Should().Be(state.Quantity);
        widget.CreatedAt.Should().Be(state.CreatedAt);
    }

    private sealed record WidgetState(
        WidgetId Id,
        string Name,
        int Quantity,
        DateTimeOffset CreatedAt
    ) : IWidgetState;
}
