using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Bundle;

namespace Acme.ApiClient;

/// <summary>
/// Builds the Kiota-generated <see cref="AcmeApiClient"/> over an existing <see cref="HttpClient"/>
/// (e.g. <c>WebApplicationFactory.CreateClient()</c>), so callers don't wire a request adapter by hand.
/// </summary>
public static class AcmeApiClientFactory
{
    /// <summary>
    /// Creates a client that sends every request through <paramref name="httpClient"/>, using its
    /// <see cref="HttpClient.BaseAddress"/> (required) as the API base URL.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="httpClient"/> has no <see cref="HttpClient.BaseAddress"/>.
    /// </exception>
    public static AcmeApiClient Create(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        // The spec has no `servers` entry, so the adapter's base URL comes only from the HttpClient.
        if (httpClient.BaseAddress is null)
        {
            throw new ArgumentException(
                "The HttpClient must have a BaseAddress pointing at the API.",
                nameof(httpClient)
            );
        }

        return new AcmeApiClient(
            new DefaultRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: httpClient)
        );
    }
}
