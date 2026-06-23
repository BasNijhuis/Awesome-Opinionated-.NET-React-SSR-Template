using Acme.Kernel.Domain.Services;

namespace Acme.Api;

/// <summary>
/// ASP.NET implementation of <see cref="ILocaleProvider"/>: reads the request's <c>Accept-Language</c>
/// (forwarded by the SSR layer) and normalizes it to a supported locale. Lives in the API composition
/// root so the modules/Infrastructure stay free of any HTTP dependency — a non-HTTP host (e.g. a CLI)
/// would register its own <see cref="ILocaleProvider"/> instead.
/// </summary>
internal sealed class HttpContextLocaleProvider(IHttpContextAccessor accessor) : ILocaleProvider
{
    private const string DefaultLocale = "en";
    private static readonly string[] Supported = ["en", "nl"];

    public string CurrentLocale
    {
        get
        {
            var header = accessor.HttpContext?.Request.Headers.AcceptLanguage.ToString();
            if (string.IsNullOrWhiteSpace(header))
            {
                return DefaultLocale;
            }

            // Take the first tag's primary subtag (e.g. "nl-NL,nl;q=0.9" -> "nl").
            var primary = header
                .Split(',')[0]
                .Split(';')[0]
                .Trim()
                .Split('-')[0]
                .ToLowerInvariant();
            return Supported.Contains(primary) ? primary : DefaultLocale;
        }
    }
}
