using Acme.ApiClient;

namespace Acme.Api.Tests;

public class GreetingsFlowTests
{
    [Fact]
    public async Task Create_then_get_then_list_round_trips_a_greeting()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = TestWebApplicationFactory.Create();
        var client = new AcmeApiClient(factory.CreateClient());

        // Act — create
        var created = await client.CreateGreetingAsync(
            new CreateGreetingRequest { Message = "hello world" },
            cancellationToken
        );

        // Act — get the same greeting back
        var fetched = await client.GetGreetingAsync(created.Id, cancellationToken);

        // Act — list
        var all = await client.ListGreetingsAsync(cancellationToken);

        // Assert
        created.Greeting.Message.Should().Be("hello world");
        fetched.Id.Should().Be(created.Id);
        fetched.Message.Should().Be("hello world");
        all.Should().ContainSingle(g => g.Id == created.Id);
    }
}
