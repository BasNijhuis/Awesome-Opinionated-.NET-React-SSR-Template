using Acme.Kernel.Domain.Services;
using Acme.Modules.Greetings.Application;

namespace Acme.Modules.Greetings.Infrastructure;

/// <summary>
/// Serves a greeting phrase from the bundled bilingual bank
/// (<see cref="GreetingSuggestionBank"/>) in the active locale
/// (<see cref="ILocaleProvider.CurrentLocale"/>) — Dutch for <c>nl</c>, English otherwise — picking
/// one at random (<see cref="IRandomProvider"/>). The localizable content lives in the data file, so
/// no display strings are hardcoded here. No database or network, so it never fails. This is the
/// worked example of localizing dynamic backend <em>content</em> via the kernel locale port (the
/// host's <c>HttpContextLocaleProvider</c> derives the locale from the request's <c>Accept-Language</c>,
/// forwarded by the SSR layer).
/// </summary>
internal sealed class LocalGreetingSuggestionProvider(
    ILocaleProvider locale,
    IRandomProvider random
) : IGreetingSuggestionProvider
{
    private readonly IReadOnlyList<BilingualSuggestion> _bank = GreetingSuggestionBank.Load();

    public string Suggest() => _bank[random.Next(_bank.Count)].For(locale.CurrentLocale);
}
