# ADR-0005: PostgreSQL via Aspire container + data volume

- **Status:** Accepted
- **Date:** 2026-06-19

## Context

Persistent state (the example modules' `Greeting` and `Widget` aggregates) requires a relational database. We want local dev to mirror production semantics without manual Postgres installation.

## Decision

Run **PostgreSQL in a Docker container** managed by Aspire:

```csharp
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .AddDatabase("acme");

var api = builder.AddProject<Projects.Acme_Api>("acme-api")
    .WithReference(postgres);
```

- Database name: `acme`
- **Data volume** persists data across AppHost restarts.
- EF Core + Npgsql wired per module: each persistent module owns a write `<M>DbContext` and a sibling no-tracking `<M>ReadDbContext` mapped to its own schema (see [ADR-0009](./0009-session-persistence-patterns.md) and [ADR-0016](./0016-aggregate-decomposition-events-per-module-persistence.md)).
- Migrations auto-applied on API startup when Postgres is enabled.

The example modules persist a `Greeting` (schema `greetings`) and a `Widget` (schema `widgets`).

## Consequences

### Positive

- No local Postgres install; consistent version for all developers.
- Connection string injected automatically into the API.
- Volume survives container restarts during development.
- Persisted state survives API/AppHost restarts.

### Negative

- **Docker must be running** before starting AppHost.
- Docker Resource Saver / unhealthy daemon blocks Postgres (Aspire warns but dashboard may still start).
- Production deployment will use a managed Postgres service, not the dev container.

### References

- `src/Aspire/Acme.AppHost/AppHost.cs`
- [ADR-0009: Persistence patterns](./0009-session-persistence-patterns.md)
- [ADR-0009](./0009-session-persistence-patterns.md) — read/write split and unit of work
