# ADR-0016: Per-aggregate, per-module persistence + domain events

- **Status:** Accepted
- **Date:** 2026-06-21
- **Amends:** [ADR-0014](./0014-modular-monolith.md) (each capability owns its own aggregate) · **Updates:** [ADR-0009](./0009-session-persistence-patterns.md) (per-module contexts + a coordinating unit of work; spec-interface hydration) · **Refines/extends:** [ADR-0015](./0015-read-data-context.md) (the `IReadDataContext` abstraction is split per module — a per-module `IReadDataContext` over each module's no-tracking read context — but reads are still composed in the Application layer)

## Context

[ADR-0014](./0014-modular-monolith.md) introduced capability modules. This ADR pins down how those modules **own their state and coordinate**: each capability owns its own aggregate, schema, and migrations, and aggregates coordinate only through in-process domain events and typed-id read ports. The kernel keeps just the shared primitives and the event contract.

| Capability | Aggregate | Owns | Schema |
|---|---|---|---|
| **Greetings** | `Greeting` | a persisted `Message` | `greetings` |
| **Widgets** | `Widget` | a `Name` + `Quantity` | `widgets` |

## Decision

### 1. Cross-aggregate reactions are in-process domain events, dispatched inside the unit of work

No aggregate mutates another's state. Instead an aggregate raises a **domain event** that lives in the shared kernel (`Kernel.Domain.DomainEvents`) so any module can handle it. The primitives are in `CQRS.Abstractions`: `IDomainEvent`, `AggregateRoot` (`RaiseEvent`/`DequeueEvents`), `IDomainEventHandler<T>`, `IDomainEventDispatcher`, `TrackedAggregates`, `DomainEventDrain`.

The **unit of work dispatches events before committing**, so a command and the reactions it triggers commit in the **same transaction**. (For example, a module could raise a `GreetingCreated` event that another module reacts to — all within one request, via `DomainEventDrain`'s cascade loop.) Where a module must drive another aggregate's lifecycle but cannot reach its module-internal write repository, it goes through a thin kernel **write port**, mirroring the read-port pattern.

> All cross-aggregate coordination is **in-process and transactional**. Integration events + an outbox (cross-process / eventual consistency) are **out of scope** — there is no cross-process consumer, and the single coordinating transaction keeps everything consistent. Revisit only if a future service split needs it.

### 2. Derived views are read projections

Anything that joins or derives across aggregates is a **projection** over the write models, not stored aggregate state. Presentation strings are derived, not persisted on aggregates.

### 3. Per-module persistence: own context, own schema, own migrations

Each **persistent** module owns its `DbContext` mapped to its **own PostgreSQL schema** with an independent migration history: Greetings → `greetings`, Widgets → `widgets`. No context maps another module's tables. Each module registers its own contexts in its `Add<Module>Module`, so the host (`Program.cs`) wires no `DbContext` at all and migrates them generically (each write context is also registered as `DbContext`; startup loops `GetServices<DbContext>()`).

A command and its reactions can span more than one context, so a **coordinating unit of work** keeps them atomic: the write contexts share one scoped connection; `AcmeUnitOfWork` opens a single transaction, enlists every `ITransactionalParticipant` (one thin adapter per write context), dispatches events so reactions stage their writes, then saves all participants and commits — rolling back everything on any failure. A user-initiated transaction is incompatible with a retrying execution strategy, so the write contexts are registered directly (`AddDbContext` + shared `NpgsqlConnection`) rather than via Aspire's enriched `AddNpgsqlDbContext`.

Reads use the **read/write split** ([ADR-0015](./0015-read-data-context.md), refined here): each module pairs its write context with a sibling **no-tracking read context** (`<M>ReadDbContext`, same mapping via a shared `<M>DbContextBase`, own connection). ADR-0015's single shared kernel `IReadDataContext` is **split per module**: each module exposes its own `IReadDataContext` marker (e.g. `IGreetingsReadContext`/`IWidgetsReadContext`) implemented over its `<M>ReadDbContext` (`Ef<M>ReadContext`, plus an `InMemory<M>ReadContext` double). The read **projections still live in the Application layer**: each module's read port composes its view via `IReadDataContext.Query<T>()` with `System.Linq` only (no `.Include` — child collections are projected inside a nested `.Select`), so adding a read shape is a one-file Application change and no Application project references EF Core (architecture-test enforced).

### 4. Cross-aggregate references by typed id only

Aggregates reference each other by **strongly-typed id value objects** — `readonly record struct` over `Guid.CreateVersion7()` (e.g. `GreetingId`, `WidgetId`) — never by object reference or cross-module foreign key. Cross-module reads go through **read ports** (kernel contracts) returning view records. A handler that needs another module's data loads its view through the port by typed id, never touching that module's internals.

### 5. The wire is unchanged

Typed ids serialize as bare `uuid` (attribute JSON converters + an OpenAPI schema transformer that inlines them), so the contract and generated clients don't change when the internal id types do.

### 6. Hydration via spec interfaces; in-memory mirrors EF

Domain aggregates rehydrate from **spec interfaces**, each owned by its module and implemented by that module's persistence entity — no parallel state records: e.g. `IGreetingState` (Greetings), `IWidgetState` (Widgets). The **in-memory test store holds the persistence entity** and rehydrates a fresh aggregate per read (writes apply eagerly, like EF's tracked entity), so the no-database path matches EF's identity-per-load semantics. Command handlers assemble their response from the no-tracking read views **after the unit of work commits**, so they see the reactions' effects without manual reloads.

### 7. Capability domain types live in their module; the kernel keeps only what is shared

Each capability's domain types live in its owning module's `Domain`. `Kernel.Domain` holds only **cross-cutting** types: the typed-id primitives and shared enums, any shared snapshot records, and the `DomainEvents/` contract. The domain→DTO mapping lives with the types, keeping `Kernel.Contracts` free of domain types. Module isolation is enforced purely by project references + the architecture tests.

## Consequences

### Positive

- Each capability owns its own state, schema, and migration history; no aggregate mutates another's internals (architecture-test enforced).
- Cross-aggregate behaviour is explicit (named events) and transactional, not hidden field-poking.
- Derived views are projections — no duplicate source of truth.
- In-memory and EF paths behave the same, so fast tests catch cross-aggregate bugs that a shared-instance store would mask.

### Negative

- More moving parts: a domain-event pipeline, a `DbContext`/schema/migration set per module, and a coordinating unit of work.
- Atomicity relies on the write contexts sharing one connection; adding a persistent module means registering its `ITransactionalParticipant` and (for now) keeping it on that shared connection.
- All coordination is in-process and transactional; there is no path for cross-process/eventual flows. That is a deliberate exclusion (no integration events or outbox) — a future service split would need to add one.

## Related

- [ADR-0014: Modular monolith](./0014-modular-monolith.md) (amended) · [ADR-0009: Persistence patterns](./0009-session-persistence-patterns.md) (updated) · [ADR-0013: Result objects + CQRS](./0013-result-objects-cqrs.md) · [ADR-0015: `IReadDataContext`](./0015-read-data-context.md)
- [backend-development.md](../instructions/backend-development.md) · [project-structure.md](../instructions/project-structure.md)
