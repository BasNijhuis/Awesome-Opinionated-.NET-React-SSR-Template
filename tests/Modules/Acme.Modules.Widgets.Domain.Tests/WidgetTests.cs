using Acme.DomainAbstractions;
using Acme.Kernel.Domain.DomainEvents;
using Acme.Modules.Widgets.Domain.Contracts;

namespace Acme.Modules.Widgets.Domain.Tests;

public sealed class WidgetTests
{
    [Fact]
    public void Create_sets_a_trimmed_name_quantity_a_fresh_id_and_a_timestamp()
    {
        // Act
        var result = Widget.Create(new CreateWidgetSpec("  Gadget  ", 7));

        // Assert
        result.IsSuccess.Should().BeTrue();
        var widget = result.Value;
        widget.Name.Should().Be("Gadget");
        widget.Quantity.Should().Be(7);
        widget.Id.Value.Should().NotBe(Guid.Empty);
        widget.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_rejects_a_blank_name()
    {
        // Act
        var result = Widget.Create(new CreateWidgetSpec("   ", 1));

        // Assert — an expected, recoverable failure (Validation), not an exception
        result.IsFailure.Should().BeTrue();
        result.Error.Category.Should().Be(ErrorCategory.Validation);
        result.Error.Code.Should().Be("Name");
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

    [Fact]
    public void AdjustQuantity_returns_a_new_instance_and_leaves_the_original_unchanged()
    {
        // Arrange
        var widget = Widget.Create(new CreateWidgetSpec("Gadget", 5)).Value;

        // Act
        var result = widget.AdjustQuantity(3);

        // Assert — immutable transition: a new instance, the original untouched (#23)
        result.IsSuccess.Should().BeTrue();
        result.Value.Quantity.Should().Be(8);
        result.Value.Should().NotBeSameAs(widget);
        widget.Quantity.Should().Be(5);
    }

    [Fact]
    public void AdjustQuantity_raises_a_WidgetQuantityAdjusted_event_with_the_before_and_after()
    {
        // Arrange
        var widget = Widget.Create(new CreateWidgetSpec("Gadget", 5)).Value;

        // Act
        var adjusted = widget.AdjustQuantity(-2).Value;

        // Assert
        var raised = adjusted.DomainEvents.OfType<WidgetQuantityAdjusted>().Single();
        raised.WidgetId.Should().Be(adjusted.Id.Value);
        raised.Name.Should().Be("Gadget");
        raised.OldQuantity.Should().Be(5);
        raised.NewQuantity.Should().Be(3);
    }

    [Fact]
    public void AdjustQuantity_rejects_an_adjustment_that_would_go_negative()
    {
        // Arrange
        var widget = Widget.Create(new CreateWidgetSpec("Gadget", 1)).Value;

        // Act
        var result = widget.AdjustQuantity(-5);

        // Assert — an expected, recoverable failure (Conflict), not an exception
        result.IsFailure.Should().BeTrue();
        result.Error.Category.Should().Be(ErrorCategory.Conflict);
        result.Error.Code.Should().Be("widget_quantity_negative");
    }

    private sealed record WidgetState(
        WidgetId Id,
        string Name,
        int Quantity,
        DateTimeOffset CreatedAt
    ) : IWidgetState;
}
