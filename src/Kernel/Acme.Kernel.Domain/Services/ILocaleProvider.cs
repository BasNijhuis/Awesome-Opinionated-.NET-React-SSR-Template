namespace Acme.Kernel.Domain.Services;

/// <summary>
/// Host-provided ambient locale, mirroring <see cref="IRandomProvider"/>: defined here so capability
/// modules can resolve a language without any HTTP/ASP.NET dependency, keeping the Application layer
/// locale-agnostic. The ASP.NET host binds this to the request's <c>Accept-Language</c>; a non-HTTP
/// host (e.g. a CLI) supplies its own implementation.
/// </summary>
public interface ILocaleProvider
{
    /// <summary>
    /// The active locale as a normalized 2-letter code — a supported value (<c>"en"</c> or
    /// <c>"nl"</c>), defaulting to <c>"en"</c>. Implementations are responsible for normalization.
    /// </summary>
    string CurrentLocale { get; }
}
