using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Acme.Api.Tests;

/// <summary>
/// The SSR layer maps a stable <c>errorCode</c> to a localized message, so every problem response
/// must carry it (see <c>ResultHttpExtensions.ToProblem</c>). These tests pin that contract.
/// </summary>
public class ErrorCodeTests
{
    private static async Task<string?> ReadErrorCode(
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return json.TryGetProperty("errorCode", out var code) ? code.GetString() : null;
    }

    [Fact]
    public async Task Unknown_greeting_returns_404_with_a_stable_error_code()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = TestWebApplicationFactory.Create();
        using var client = factory.CreateClient();

        // Act — a well-formed but non-existent greeting id
        var response = await client.GetAsync($"/api/greetings/{Guid.NewGuid()}", cancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadErrorCode(response, cancellationToken)).Should().NotBeNullOrEmpty();
    }
}
