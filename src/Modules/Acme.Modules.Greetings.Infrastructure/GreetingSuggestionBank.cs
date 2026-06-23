using System.Reflection;
using System.Text.Json;

namespace Acme.Modules.Greetings.Infrastructure;

/// <summary>A suggestion in both supported languages; <see cref="For"/> picks the active one.</summary>
public sealed record BilingualSuggestion(string En, string Nl)
{
    public string For(string locale) =>
        locale.Equals("nl", StringComparison.OrdinalIgnoreCase) ? Nl : En;
}

/// <summary>
/// Loads the bundled bilingual greeting-suggestion bank (<c>greeting-suggestions.json</c>, an embedded
/// resource) once. Keeping the localizable content in a data file — not hardcoded in C# — means no
/// display strings live in handlers/providers; the provider only selects the locale-appropriate entry.
/// </summary>
public static class GreetingSuggestionBank
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Lazy<IReadOnlyList<BilingualSuggestion>> Cached = new(LoadFromResource);

    public static IReadOnlyList<BilingualSuggestion> Load() => Cached.Value;

    private static IReadOnlyList<BilingualSuggestion> LoadFromResource()
    {
        var assembly = typeof(GreetingSuggestionBank).Assembly;
        var resourceName =
            assembly
                .GetManifestResourceNames()
                .FirstOrDefault(n =>
                    n.EndsWith("greeting-suggestions.json", StringComparison.Ordinal)
                )
            ?? throw new InvalidOperationException(
                "Embedded suggestion bank 'greeting-suggestions.json' not found."
            );

        using var stream =
            assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Could not open resource stream '{resourceName}'."
            );

        var bank =
            JsonSerializer.Deserialize<SuggestionBankFile>(stream, JsonOptions)
            ?? throw new InvalidOperationException("Suggestion bank deserialized to null.");

        return bank.Suggestions;
    }

    private sealed record SuggestionBankFile(IReadOnlyList<BilingualSuggestion> Suggestions);
}
