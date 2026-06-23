using Acme.ApiClient;

namespace Acme.Api.Tests;

/// <summary>
/// End-to-end proof of the immutable update + cross-module domain event: adjusting a widget's quantity
/// (a <c>Widget.AdjustQuantity</c> transition that raises <c>WidgetQuantityAdjusted</c>) makes the
/// <strong>Greetings</strong> module record an announcement greeting in the same transaction (ADR-0016).
/// </summary>
public class WidgetQuantityAdjustmentFlowTests
{
    [Fact]
    public async Task Adjusting_a_widget_updates_its_quantity_and_announces_it_via_a_greeting()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = TestWebApplicationFactory.Create();
        var client = new AcmeApiClient(factory.CreateClient());

        var created = await client.CreateWidgetAsync(
            new CreateWidgetRequest { Name = "Gadget", Quantity = 5 },
            cancellationToken
        );

        // Act — a widget command raises a cross-module event the Greetings module reacts to
        var adjusted = await client.AdjustWidgetQuantityAsync(
            created.Id,
            new AdjustWidgetQuantityRequest { Delta = 3 },
            cancellationToken
        );

        // Assert — the widget is updated...
        adjusted.Quantity.Should().Be(8);

        // ...and the reaction committed a greeting in the same transaction
        var greetings = await client.ListGreetingsAsync(cancellationToken);
        greetings
            .Should()
            .ContainSingle(g => g.Message == "Widget 'Gadget' quantity changed from 5 to 8.");
    }

    [Fact]
    public async Task Adjusting_below_zero_is_rejected_and_creates_no_greeting()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = TestWebApplicationFactory.Create();
        var client = new AcmeApiClient(factory.CreateClient());

        var created = await client.CreateWidgetAsync(
            new CreateWidgetRequest { Name = "Gadget", Quantity = 1 },
            cancellationToken
        );

        // Act — the domain rule rejects this (would go negative)
        var act = async () =>
            await client.AdjustWidgetQuantityAsync(
                created.Id,
                new AdjustWidgetQuantityRequest { Delta = -5 },
                cancellationToken
            );

        // Assert — a 409 Conflict, and no announcement greeting was written (transaction rolled back)
        var error = await act.Should().ThrowAsync<ApiException>();
        error.Which.StatusCode.Should().Be(409);

        var greetings = await client.ListGreetingsAsync(cancellationToken);
        greetings.Should().BeEmpty();
    }
}
