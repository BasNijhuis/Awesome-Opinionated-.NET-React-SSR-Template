using Acme.DomainAbstractions;
using Acme.Kernel.Domain.DomainEvents;
using Acme.Modules.Widgets.Domain.Contracts;
using Acme.TestSupport;

namespace Acme.Modules.Widgets.Domain.Tests;

public sealed class WidgetTests
{
    [Fact]
    public void Create_sets_a_trimmed_name_quantity_a_fresh_id_and_a_timestamp()
    {
        // Act
        var result = Widget.Create(new CreateWidgetSpec("  Gadget  ", 7));

        // Assert
        var widget = result.ShouldSucceed();
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
        var error = result.ShouldFail().Error;
        error.Category.Should().Be(ErrorCategory.Validation);
        error.Code.Should().Be("Name");
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
        var widget = Widget.Create(new CreateWidgetSpec("Gadget", 5)).ShouldSucceed();

        // Act
        var result = widget.AdjustQuantity(3);

        // Assert — immutable transition: a new instance, the original untouched (#23)
        var adjusted = result.ShouldSucceed();
        adjusted.Quantity.Should().Be(8);
        adjusted.Should().NotBeSameAs(widget);
        widget.Quantity.Should().Be(5);
    }

    [Fact]
    public void AdjustQuantity_raises_a_WidgetQuantityAdjusted_event_with_the_before_and_after()
    {
        // Arrange
        var widget = Widget.Create(new CreateWidgetSpec("Gadget", 5)).ShouldSucceed();

        // Act
        var adjusted = widget.AdjustQuantity(-2).ShouldSucceed();

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
        var widget = Widget.Create(new CreateWidgetSpec("Gadget", 1)).ShouldSucceed();

        // Act
        var result = widget.AdjustQuantity(-5);

        // Assert — an expected, recoverable failure (Conflict), not an exception
        var error = result.ShouldFail().Error;
        error.Category.Should().Be(ErrorCategory.Conflict);
        error.Code.Should().Be("widget_quantity_negative");
    }

    private sealed record WidgetState(
        WidgetId Id,
        string Name,
        int Quantity,
        DateTimeOffset CreatedAt
    ) : IWidgetState;
}
