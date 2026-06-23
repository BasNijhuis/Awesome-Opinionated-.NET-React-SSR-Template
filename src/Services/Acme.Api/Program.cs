using System.Text.Json.Serialization;
using Acme.Api;
using Acme.Api.Realtime;
using Acme.Http;
using Acme.Kernel.Application;
using Acme.Kernel.Domain.Services;
using Acme.Kernel.Infrastructure;
using Acme.Modules.Greetings.Endpoints;
using Acme.Modules.Greetings.Infrastructure;
using Acme.Modules.Widgets.Endpoints;
using Acme.Modules.Widgets.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Kernel
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Ambient locale from the request's Accept-Language (forwarded by SSR) — host owns the HTTP binding
// so modules/Infrastructure stay HTTP-agnostic (ILocaleProvider lives in Kernel.Domain.Services).
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ILocaleProvider, HttpContextLocaleProvider>();

// Modules — each owns its handlers, persistence (schema + migrations) and endpoints. Adding a module
// is two lines here (Add…Module + Map…Endpoints); see docs/instructions/project-structure.md.
builder.Services.AddGreetingsModule(builder.Configuration);
builder.Services.AddWidgetsModule(builder.Configuration);

builder.Services.AddApiServices();

// Serialize enums as their string names so the contract (and OpenAPI schemas) use names, and
// DateTimeOffset values as canonical UTC ("…Z") so they satisfy the frontend's strict date-time
// contract validation.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.Converters.Add(new UtcDateTimeOffsetConverter());
});

var app = builder.Build();

// Migrate only when a real relational database is configured. This is skipped for the in-memory
// test host and during build-time OpenAPI document generation (the GetDocument tool boots the app
// but has no connection string), so `dotnet build` can export the spec without a database.
if (
    !app.Configuration.GetValue("Persistence:UseInMemory", false)
    && app.Configuration.GetConnectionString("acme") is not null
)
{
    using var scope = app.Services.CreateScope();
    // Each module registers its write context as a DbContext, so the host migrates every schema
    // without naming any of them.
    foreach (var context in scope.ServiceProvider.GetServices<DbContext>())
    {
        context.Database.Migrate();
    }
}

app.UseDomainExceptionHandler();
app.MapOpenApi();
app.MapDefaultEndpoints();
app.MapGet("/api/ping", () => Results.Ok(new { status = "ok" }));
app.MapGreetingsEndpoints();
app.MapWidgetsEndpoints();
app.MapHub<NotificationsHub>("/hubs/notifications");

app.Run();

/// <summary>Exposed so the API integration tests can reference the entry point via WebApplicationFactory.</summary>
public partial class Program;
