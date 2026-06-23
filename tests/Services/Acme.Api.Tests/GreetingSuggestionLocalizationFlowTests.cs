using System.Net.Http.Json;

namespace Acme.Api.Tests;

/// <summary>
/// Proves backend content localization via the kernel <c>ILocaleProvider</c>: GET /api/greetings/suggestion
/// returns a phrase from the bundled bilingual bank in the locale derived from <c>Accept-Language</c>
/// (the header the SSR layer forwards). No display strings are composed in handlers — the content lives
/// in the embedded data file.
/// </summary>
public class GreetingSuggestionLocalizationFlowTests
{
    // Mirrors the embedded greeting-suggestions.json bank.
    private static readonly string[] English =
    [
        "Hello there!",
        "Good to see you!",
        "Welcome!",
        "Have a great day!",
        "Greetings, friend!",
    ];
    private static readonly string[] Dutch =
    [
        "Hallo daar!",
        "Goed je te zien!",
        "Welkom!",
        "Fijne dag!",
        "Gegroet, vriend!",
    ];

    [Fact]
    public async Task Suggestion_is_returned_in_Dutch_when_Accept_Language_is_nl()
    {
        var suggestion = await SuggestAsync(acceptLanguage: "nl-NL,nl;q=0.9");
        Dutch.Should().Contain(suggestion);
        English.Should().NotContain(suggestion);
    }

    [Fact]
    public async Task Suggestion_defaults_to_English_when_no_locale_is_provided()
    {
        var suggestion = await SuggestAsync(acceptLanguage: null);
        English.Should().Contain(suggestion);
    }

    private static async Task<string> SuggestAsync(string? acceptLanguage)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = TestWebApplicationFactory.Create();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/greetings/suggestion");
        if (acceptLanguage is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage);
        }

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<SuggestionResponse>(cancellationToken);
        return dto!.Suggestion;
    }

    private sealed record SuggestionResponse(string Suggestion);
}
