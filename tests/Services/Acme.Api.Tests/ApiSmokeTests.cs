using System.Net.Http.Json;

namespace Acme.Api.Tests;

public class ApiSmokeTests
{
    [Fact]
    public async Task Ping_returns_ok()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = TestWebApplicationFactory.Create();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/ping", cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<PingResponse>(cancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        payload.Should().NotBeNull();
        payload!.Status.Should().Be("ok");
    }

    [Fact]
    public async Task OpenApi_document_is_served()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = TestWebApplicationFactory.Create();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/openapi/v1.json", cancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
    }

    private sealed record PingResponse(string Status);
}
