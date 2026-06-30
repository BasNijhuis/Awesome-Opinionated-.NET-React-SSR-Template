# ADR-0001: Use .NET Aspire for local orchestration

- **Status:** Accepted
- **Date:** 2026-06-19

## Context

This is a multi-service app: PostgreSQL, an ASP.NET Core API, and a Node-based SSR frontend. Running each component manually (separate terminals, hand-wired URLs, no shared telemetry) slows development and hides integration issues.

We target **.NET 10** and want first-class service discovery, health checks, and a unified dashboard during local development.

## Decision

Use **.NET Aspire 13.4.x** with a single **AppHost** project (`Acme.AppHost`) as the source of truth for:

- PostgreSQL container (`AddPostgres` + data volume)
- API project (`AddProject<Acme_Api>`)
- React Router web app (`AddJavaScriptApp` + `WithPnpm`)

Start the full stack with:

```powershell
dotnet run --project src/Aspire/Acme.AppHost
```

Shared cross-cutting defaults live in `Acme.ServiceDefaults` (OpenTelemetry, health checks, service discovery).

## Consequences

### Positive

- One command starts Postgres, API, and web together.
- Aspire dashboard shows logs, traces, and resource health.
- Service references inject connection strings and internal URLs automatically.
- Aligns with cloud-native deployment patterns later (`aspire publish`).

### Negative

- Requires **Docker Desktop** (or compatible runtime) for Postgres locally.
- Developers must understand AppHost is the entry point, not individual projects.
- Aspire package versions must stay aligned.

### References

- `src/Aspire/Acme.AppHost/AppHost.cs`
- [Instructions: Local development](../instructions/local-development.md)
