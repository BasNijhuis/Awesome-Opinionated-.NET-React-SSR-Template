namespace Acme.Modules.Greetings.Application;

/// <summary>
/// Supplies a greeting phrase in the caller's active locale. A port so the Application layer stays off
/// the localization mechanism: the implementation (Greetings.Infrastructure) reads the ambient locale
/// from the kernel <c>ILocaleProvider</c> and serves the matching text — the template's example of
/// localizing dynamic content from the backend.
/// </summary>
public interface IGreetingSuggestionProvider
{
    /// <summary>A localized greeting phrase (which one may vary between calls).</summary>
    string Suggest();
}
