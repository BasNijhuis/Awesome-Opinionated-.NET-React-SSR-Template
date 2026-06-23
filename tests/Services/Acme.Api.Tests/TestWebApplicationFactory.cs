using Microsoft.AspNetCore.Mvc.Testing;

namespace Acme.Api.Tests;

internal static class TestWebApplicationFactory
{
    public static WebApplicationFactory<Program> Create() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Run the modules against the in-memory persistence path so the suite needs no Docker.
            builder.UseSetting("Persistence:UseInMemory", "true");
        });
}
