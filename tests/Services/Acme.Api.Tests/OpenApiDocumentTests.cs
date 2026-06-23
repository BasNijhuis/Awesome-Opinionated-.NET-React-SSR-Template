using System.Text.Json;

namespace Acme.Api.Tests;

public class OpenApiDocumentTests
{
    private static readonly string[] ExpectedPaths =
    [
        "/api/greetings",
        "/api/greetings/{id}",
        "/api/widgets",
        "/api/widgets/{id}",
    ];

    [Fact]
    public async Task Document_is_served_at_openapi_v1_json()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = TestWebApplicationFactory.Create();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json", cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Document_lists_all_module_operations()
    {
        using var document = await GetDocumentAsync();
        var paths = document.RootElement.GetProperty("paths");

        foreach (var path in ExpectedPaths)
        {
            paths
                .TryGetProperty(path, out _)
                .Should()
                .BeTrue("OpenAPI document is missing path '{0}'.", path);
        }
    }

    [Fact]
    public async Task Document_exposes_typed_response_schemas()
    {
        using var document = await GetDocumentAsync();
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        schemas.TryGetProperty("GreetingDto", out _).Should().BeTrue("Missing GreetingDto schema.");
        schemas.TryGetProperty("WidgetDto", out _).Should().BeTrue("Missing WidgetDto schema.");
        schemas
            .TryGetProperty("CreateGreetingResult", out _)
            .Should()
            .BeTrue("Missing CreateGreetingResult schema.");
    }

    [Fact]
    public async Task Typed_ids_serialize_as_uuid_strings()
    {
        // GreetingId / WidgetId are strongly-typed wrappers over Guid; on the wire they must surface as
        // a bare uuid string, never as the typed-id struct.
        using var document = await GetDocumentAsync();
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        var greetingId = schemas
            .GetProperty("GreetingDto")
            .GetProperty("properties")
            .GetProperty("id");
        greetingId.GetProperty("type").GetString().Should().Be("string");
        greetingId.GetProperty("format").GetString().Should().Be("uuid");

        var widgetId = schemas.GetProperty("WidgetDto").GetProperty("properties").GetProperty("id");
        widgetId.GetProperty("type").GetString().Should().Be("string");
        widgetId.GetProperty("format").GetString().Should().Be("uuid");
    }

    [Fact]
    public async Task Integer_properties_are_plain_integers_not_string_unions()
    {
        // Quantity is an int; it must be "integer", not the "integer | string" union that an
        // unconfigured number policy would produce.
        using var document = await GetDocumentAsync();
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        var quantity = schemas
            .GetProperty("WidgetDto")
            .GetProperty("properties")
            .GetProperty("quantity");

        quantity.GetProperty("type").GetString().Should().Be("integer");
        quantity.GetProperty("format").GetString().Should().Be("int32");
    }

    private static async Task<JsonDocument> GetDocumentAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = TestWebApplicationFactory.Create();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json", cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(json);
    }
}
