using Acme.ApiClient;

namespace Acme.Api.Tests;

public class WidgetsFlowTests
{
    [Fact]
    public async Task Create_then_get_then_list_round_trips_a_widget()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = TestWebApplicationFactory.Create();
        var client = new AcmeApiClient(factory.CreateClient());

        // Act — create
        var created = await client.CreateWidgetAsync(
            new CreateWidgetRequest { Name = "Gadget", Quantity = 7 },
            cancellationToken
        );

        // Act — get the same widget back
        var fetched = await client.GetWidgetAsync(created.Id, cancellationToken);

        // Act — list
        var all = await client.ListWidgetsAsync(cancellationToken);

        // Assert
        created.Widget.Name.Should().Be("Gadget");
        created.Widget.Quantity.Should().Be(7);
        fetched.Id.Should().Be(created.Id);
        fetched.Name.Should().Be("Gadget");
        fetched.Quantity.Should().Be(7);
        all.Should().ContainSingle(w => w.Id == created.Id);
    }
}
