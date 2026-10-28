using Acme.ApiClient;
using Acme.ApiClient.Models;

namespace Acme.Api.Tests;

public class WidgetsFlowTests
{
    [Fact]
    public async Task Create_then_get_then_list_round_trips_a_widget()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = TestWebApplicationFactory.Create();
        var client = AcmeApiClientFactory.Create(factory.CreateClient());

        // Act — create
        var created = await client.Api.Widgets.PostAsync(
            new CreateWidgetRequest { Name = "Gadget", Quantity = 7 },
            cancellationToken: cancellationToken
        );
        var createdId = created!.Id!.Value;

        // Act — get the same widget back
        var fetched = await client
            .Api.Widgets[createdId]
            .GetAsync(cancellationToken: cancellationToken);

        // Act — list
        var all = await client.Api.Widgets.GetAsync(cancellationToken: cancellationToken);

        // Assert
        created.Widget!.Name.Should().Be("Gadget");
        created.Widget.Quantity.Should().Be(7);
        fetched!.Id.Should().Be(createdId);
        fetched.Name.Should().Be("Gadget");
        fetched.Quantity.Should().Be(7);
        all.Should().ContainSingle(w => w.Id == createdId);
    }
}
