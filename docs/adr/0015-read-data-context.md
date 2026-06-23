# ADR-0015: Application-composed read projections via `IReadDataContext`

- **Status:** Accepted
- **Date:** 2026-06-21
- **Refines:** [ADR-0009](./0009-session-persistence-patterns.md) (read/write split, `AsNoTracking`), [ADR-0014](./0014-modular-monolith.md) (module boundaries; no Application → Infrastructure dependency)

## Context

Reads used to go through capability-specific query ports (Application), implemented in Infrastructure. Every new read shape required **a new method in Infrastructure** — even a trivial projection — and the EF query (`Where`/`Select`/`Include`) lived in the infra layer where the application slice that needs it could not shape it. Reads also often rehydrated a full aggregate just to map it to a DTO.

We want application handlers to build **optimized read projections themselves**, so adding a query is a one-file change in the owning module's Application layer with no Infrastructure round-trip.

## Decision

1. **`IReadDataContext` hands the application an `IQueryable<T>` for composition and executes it via members.** Handlers compose `Where`/`Select`/pagination with `System.Linq` only; the EF Core async extension methods (`ToListAsync`/`FirstOrDefaultAsync`/`AnyAsync`) stay in Infrastructure behind the interface:

   ```csharp
   IQueryable<T> Query<T>() where T : class;            // AsNoTracking source for composition
   Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken ct);
   Task<T?>      FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken ct);
   Task<bool>    AnyAsync<T>(IQueryable<T> query, CancellationToken ct);
   ```

   No application project references `Microsoft.EntityFrameworkCore` — enforced by an architecture test (`tests/Acme.Architecture.Tests`).

2. **The persistence entities live in the Application layer** (`*Entity` POCOs). They are plain POCOs owned by the application: the write path maps the aggregate to/from them and read queries project off them. Their **EF Core mapping/configuration stays in Infrastructure** (`OnModelCreating`). These are distinct from *read-model projections* — the flat shapes a handler `.Select`s into.

3. **A read handler projects, then composes the DTO.** A handler projects a flat read model (raw columns + child lists) through `IReadDataContext`, then builds the DTO in memory, reusing the shared mapper so that logic lives in one place. No full-aggregate rehydration.

The in-memory test double (`InMemoryReadDataContext`) is generic over a DI-registered projector per entity type, so adding a read source is a registration, not a context edit.

## Consequences

- Adding a read is a one-file change in the owning module's Application layer; projections fetch only the columns a screen needs and never round-trip through Infrastructure.
- The application owns the persistence entity shapes; Infrastructure owns only their EF mapping. The Application↔EF boundary is enforced by an architecture test.
- Splitting the read context per module (schema-per-module) is realized in [ADR-0016](./0016-aggregate-decomposition-events-per-module-persistence.md): the single kernel `IReadDataContext` became a **per-module `IReadDataContext`** (one per module, over that module's no-tracking `<M>ReadDbContext`), and the Application-composed read projections moved into each module's read port — this ADR's abstraction is kept, just split per module.
