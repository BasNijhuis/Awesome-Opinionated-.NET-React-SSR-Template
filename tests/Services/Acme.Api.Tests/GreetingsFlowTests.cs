using Acme.ApiClient;
using Acme.ApiClient.Models;

namespace Acme.Api.Tests;

public class GreetingsFlowTests
{
    [Fact]
    public async Task Create_then_get_then_list_round_trips_a_greeting()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = TestWebApplicationFactory.Create();
        var client = AcmeApiClientFactory.Create(factory.CreateClient());

        // Act — create
        var created = await client.Api.Greetings.PostAsync(
            new CreateGreetingRequest { Message = "hello world" },
            cancellationToken: cancellationToken
        );
        var createdId = created!.Id!.Value;

        // Act — get the same greeting back
        var fetched = await client
            .Api.Greetings[createdId]
            .GetAsync(cancellationToken: cancellationToken);

        // Act — list
        var all = await client.Api.Greetings.GetAsync(cancellationToken: cancellationToken);

        // Assert
        created.Greeting!.Message.Should().Be("hello world");
        fetched!.Id.Should().Be(createdId);
        fetched.Message.Should().Be("hello world");
        all.Should().ContainSingle(g => g.Id == createdId);
    }
}
