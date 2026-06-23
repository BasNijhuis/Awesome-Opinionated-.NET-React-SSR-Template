using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// End-to-end tests (Playwright, #12) need a deterministic web URL and the production server (so the
// `/hubs` proxy in `server.js` is exercised, not the dev-only `vite.config.ts` proxy). When
// `E2E_WEB_PORT` is set the web runs the built production server on that fixed port; otherwise it
// stays the hot-reloading dev server on a dynamically allocated port — normal dev/prod is unchanged.
var e2eWebPort = builder.Configuration.GetValue<int?>("E2E_WEB_PORT");
var isE2E = e2eWebPort is not null;
var webRunScript = isE2E ? "start" : "dev";
var webNodeEnv = isE2E ? "production" : "development";

// Pin a deterministic password. Postgres only applies POSTGRES_PASSWORD when it first initializes an
// empty data dir, so the persisted data volume bakes in whatever password created it. A generated
// password drifts (cleared user secrets, a fresh clone, CI, a teammate, or switching to/from E2E's
// ephemeral database), leaving the reused volume unauthenticatable. A fixed value keeps the volume
// and the API in sync across every run, machine, and mode. Local-dev only — real environments inject
// the connection string, never this parameter.
var dbPassword = builder.AddParameter("postgres-password", "acme-dev", secret: true);
var postgres = builder.AddPostgres("postgres", password: dbPassword);
if (!isE2E)
{
    // Persist data across local/dev runs. E2E instead uses a fresh ephemeral database each run for
    // clean, isolated state and a faster teardown.
    postgres = postgres.WithDataVolume();
}

var database = postgres.AddDatabase("acme");

var api = builder
    .AddProject<Projects.Acme_Api>("acme-api")
    .WithReference(database)
    .WaitFor(database)
    .WithHttpHealthCheck("/health");

const string webAppPath = "../../Services/acme-web";
var isRunMode = builder.ExecutionContext.IsRunMode;

var web = builder
    .AddJavaScriptApp("acme-web", webAppPath)
    // In run mode the pre-step (below) installs packages; otherwise install here.
    .WithPnpm(install: !isRunMode)
    .WithRunScript(webRunScript)
    .WithReference(api)
    // Explicitly inject the API's HTTP URL as API_URL. `WithReference` alone doesn't reliably surface
    // the `services__acme-api__http__0` discovery var to the production `node server.js`
    // process (it's empty on CI), and both the SSR client (config.server.ts) and the /hubs proxy
    // (server.js) read API_URL as the fallback. HTTP (not HTTPS) keeps the internal call cert-free.
    .WithEnvironment("API_URL", api.GetEndpoint("http"))
    .WithHttpEndpoint(port: e2eWebPort, env: "PORT")
    .WithExternalHttpEndpoints()
    .WithEnvironment("NODE_ENV", webNodeEnv)
    .WaitFor(api);

if (isRunMode)
{
    // One-shot pre-step: install packages, then prepare the web build before the server starts.
    // Normally that's just `api:generate` (OpenAPI client + Zod contract types) for the dev server;
    // in E2E mode it's a full `build` (which runs `api:generate` then `react-router build`) so the
    // production server has `build/` to serve. Waits for the API so its `dotnet build` is a no-op.
    var webPrepScript = isE2E ? "build" : "api:generate";
    var webPrepare = builder
        .AddJavaScriptApp("acme-web-prepare", webAppPath)
        .WithPnpm()
        .WithRunScript(webPrepScript)
        .WaitFor(api);

    web.WaitForCompletion(webPrepare);
}

builder.Build().Run();
