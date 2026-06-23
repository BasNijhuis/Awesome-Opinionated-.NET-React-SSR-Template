# ADR-0009: Persistence — read/write split, unit of work, staged writes

- **Status:** Accepted
- **Date:** 2026-06-19

## Context

Persisted aggregates must survive API restarts (PostgreSQL via Aspire). Command handlers (POST/PATCH) mutate an aggregate; GET handlers read projections frequently. We need:

- Clear separation between reads and writes
- One transactional commit per HTTP command
- Repositories injectable on their own (not exposed through the unit of work)
- Domain types never mapped directly by EF

> **Updated by [ADR-0016](./0016-aggregate-decomposition-events-per-module-persistence.md):** the patterns
> below are now applied **per module** — each persistent module owns its write/read contexts, schema, and
> migrations, and a coordinating unit of work commits them together. The single-context shape shown here
> was the original design; the read/write split, staged writes, and "domain types are never EF entities"
> rules carry over unchanged.

## Decision

### 1. Read/write split (CQRS-lite)

| Concern | Abstraction | DbContext | Tracking |
|---------|-------------|-----------|----------|
| **GET / queries** | a module read port (e.g. `IGreetingReader`) | `<M>ReadDbContext` | `NoTracking` |
| **Commands** | the module's aggregate repo (e.g. `IGreetingRepository`) | `<M>DbContext` | Staged on commit |

A module's write and read contexts share the same mapping via a `<M>DbContextBase`. Read contexts are no-tracking:

```csharp
builder.AddNpgsqlDbContext<GreetingsReadDbContext>(
    "acme",
    configureDbContextOptions: o => o.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
```

GET handlers use the read port only. All mutation handlers use the write repository.

### 2. Unit of work — commit only

`IUnitOfWork` exposes **only** `SaveChangesAsync`. It does **not** expose repositories.

Handlers inject **both** the aggregate repository and `IUnitOfWork`:

```csharp
var widget = await repository.GetByIdAsync(widgetId, ct) ...;
var adjusted = widget.AdjustQuantity(delta);
repository.Update(adjusted.Value);
await unitOfWork.SaveChangesAsync(ct);
```

`AcmeUnitOfWork` opens a transaction and commits the write context(s). Repositories register adds/updates on the change tracker when `Add` / `Update` are called.

### 3. Staged writes (sync `Add` / `Update`)

Repository methods **stage** changes; they do not hit the database:

```csharp
void Add(Greeting greeting);
void Update(Greeting greeting);
Task<Greeting?> GetByIdAsync(GreetingId id, CancellationToken ct);  // async read for commands
```

On `Update`, the repository loads the tracked entity graph and applies the persistence mapper immediately. `SaveChangesAsync` persists the change tracker in one transaction.

### 4. Aggregate mapping

- Persistence entities live in the Application layer (`*Entity` POCOs) — not domain types.
- Aggregates rehydrate from **spec interfaces** (`I*State`) implemented by those entities; the write path maps the aggregate to/from them.
- Domain types are **never** EF entities — enforced by the `ACME006` analyzer rule (see [ADR-0017](./0017-immutable-domain-enforced-by-analyzer.md)).

### 5. Migrations and environments

- Each persistent module owns its migration history under `Persistence/Migrations`.
- `Database.Migrate()` on API startup whenever Postgres is enabled (Development and Production).
- Design-time uses each context's `DbContextFactory`.

### 6. Tests vs production

| Mode | Config | Storage |
|------|--------|---------|
| **Aspire / local** | default | PostgreSQL |
| **API integration tests** | `Persistence:UseInMemory=true` | In-memory store (scoped repo + UoW) |
| **EF integration tests** | Testcontainers Postgres | Real EF pipeline |

## Consequences

### Positive

- GET reads do not pollute the write change tracker.
- One explicit commit per command; any external I/O completes before `SaveChangesAsync`.
- Repositories and UoW are independently injectable and testable.
- API tests stay fast without Postgres.

### Negative

- Two DbContext registrations per request scope (read + write) per module.
- Handlers must remember to call `SaveChangesAsync` after staging.
- In-memory tests use an in-memory store; EF uses the change tracker directly.

### References

- [ADR-0005](./0005-postgresql-via-aspire.md) — Postgres container
- [ADR-0002](./0002-clean-architecture-vertical-slices.md) — layer boundaries
- [ADR-0016](./0016-aggregate-decomposition-events-per-module-persistence.md) — per-module persistence
- [backend-development.md](../instructions/backend-development.md) — handler patterns
