# Backend development

> The backend is a **modular monolith** ([ADR-0014](../adr/0014-modular-monolith.md)): capability modules
> (`Greetings`/`Widgets` in this template) over a shared kernel, with NetArchTest-enforced boundaries.
> See [project-structure.md](./project-structure.md#modules--boundaries-adr-0014) for the layout and the
> "how to add a module" recipe.

## Layers (where to put code)

Within a module, the clean-architecture layers are separate projects:

| Layer | Project | Put here | Visibility |
|-------|---------|----------|-----------|
| **Domain** | `Modules.<M>.Domain` | the module's aggregate + its domain rules (e.g. `Greeting`, `Widget`) | internal |
| **Domain.Contracts** | `Modules.<M>.Domain.Contracts` | the domain's input **spec interfaces** (`I…Spec`) — see [ADR-0018](../adr/0018-domain-spec-interfaces-contract-layering.md). References only `Kernel.Domain` | **public** |
| **Application** | `Modules.<M>.Application` | handlers, validators, ports, mappers | internal |
| **Infrastructure** | `Modules.<M>.Infrastructure` | adapters + `Add<M>Module()` DI | internal |
| **Application.Contracts** | `Modules.<M>.Application.Contracts` | commands/queries + result DTOs (implement the `I…Spec` from `Domain.Contracts`; **must not reference the module `Domain`**) | **public** |
| **Endpoints** | `Modules.<M>.Endpoints` | `Map<M>Endpoints()` + request DTOs | public |

The **shared kernel** (`Acme.Kernel.*`) holds only what is shared — no capability aggregate (the aggregates live in their modules; [ADR-0016](../adr/0016-aggregate-decomposition-events-per-module-persistence.md)): the cross-cutting primitives — typed ids/enums and the `DomainEvents/` contract (`Domain`); the read/write ports + `IReadDataContext` + the realtime `INotificationPublisher` + hub topics (`Application`); and the shared cross-module DTOs such as `NotificationMessage` (`Contracts`). `Result`/`Error` live in the `Acme.DomainAbstractions` building block. **Building blocks** (`Acme.{CQRS,CQRS.Abstractions,Http,DomainAbstractions,DomainAnalyzers}`) hold the dispatcher (`AddCqrs`/`AddCqrsHandlersFrom`), CQRS/validation/domain-event abstractions (incl. `AggregateRoot`, `IDomainEventDispatcher`, `TrackedAggregates`, `ITransactionalParticipant`), and the shared `ToHttpResult` endpoint helper (see [ADR-0013](../adr/0013-result-objects-cqrs.md)).

**Boundary rules (build-enforced):** a module references only the shared kernel/building blocks + other modules' contract projects (`.Application.Contracts`/`.Domain.Contracts`) — never another module's internals. Within a module, `.Application.Contracts` references `.Domain.Contracts` (input spec ports) but never the module `.Domain` ([ADR-0018](../adr/0018-domain-spec-interfaces-contract-layering.md)). Cross-module reactions go through the dispatcher, an in-process domain event, or a kernel port. The kernel depends on no module; `Domain` depends only on `DomainAbstractions`. Never reference EF/ASP.NET/HTTP from `Domain`.

**Domain immutability (build-enforced, [ADR-0017](../adr/0017-immutable-domain-enforced-by-analyzer.md)):** every Domain type is immutable — **no `set` accessor anywhere** (`init`/`readonly` only; collections exposed as `IReadOnly…`), and types are `record`/`record struct`/`interface`/`enum` (no plain `class`/`struct`, except `static` utilities + framework-derived classes like `Exception`/`JsonConverter<T>`). Aggregates are `sealed record`s; a transition returns a **new** instance (`var next = this with { … }; return Result.Success(next.RaiseEvent<Widget>(…));`) and the handler reassigns it (`widget = result.Value; repository.Update(widget);`). The domain-events outbox is immutable too — `RaiseEvent<TSelf>` and `DequeueEvents()` (returns `(events, cleared)`) are functional; the unit of work tracks by `AggregateRoot.HasSameIdentity` and swaps in cleared copies. Mutable enumerable types (`List<>`/`Dictionary<>`/`T[]`/…) are banned in Domain signatures/members — use `IReadOnly…`/`ImmutableArray<>`. The `Acme.DomainAnalyzers` analyzer fails the build (`ACME001` mutable state · `ACME002` record-only · `ACME003` no mutable enumerables · `ACME004` `RaiseEvent<TSelf>` must pass the calling type · `ACME005` no **public `init`**) on any violation in a Domain project.

**EF entity boundary (build-enforced, `ACME006`):** in `*.Infrastructure` projects, the same analyzer forbids mapping a **domain type** (anything in a `.Domain`/`.DomainAbstractions` assembly/namespace) as an EF entity — i.e. as the type argument of `DbSet<T>`/`Set<T>`/`Entity`/`HasOne`/`HasMany`/`OwnsOne`/`OwnsMany`/`ComplexProperty`/`Navigation`, or `Entity(typeof(T))`. Map the Application-layer `*Entity` POCO instead (ADR-0016/0018). Value-object `Property(…).HasConversion(…)` mappings are exempt. Auto-wired via `Directory.Build.targets` (`EnforceEfEntityBoundary` on `.Infrastructure`).

**Construction: non-public ctor + factory ([ADR-0017](../adr/0017-immutable-domain-enforced-by-analyzer.md)).** Domain class records are built through a non-public constructor plus a `static` factory (`Create`/`Rehydrate`, or `Error.NotFound`/etc.) — `init` is never public (`ACME005`). `readonly record struct` value objects (`GreetingId`/`WidgetId`/…) stay positional (C# forces a public parameterless ctor on structs; their generated `init` is exempt). A `jsonb`-persisted carrier carries `[JsonConstructor]` on its private ctor.

**Domain method inputs: spec interfaces ([ADR-0018](../adr/0018-domain-spec-interfaces-contract-layering.md)).** A public domain method with **more than one data parameter** takes a single `I…Spec` interface (defined in `Modules.<M>.Domain.Contracts`, or `Kernel.Domain` for event-driven ones); injected services and any shared snapshot stay as separate params. When the spec data is fully endpoint-provided, the command model implements the spec directly (`CreateGreetingCommand : ICommand<…>, ICreateGreetingSpec`); when the method also needs **handler-derived** data (a generated id, a default, a value read from another aggregate), the handler builds a `private sealed record … : I…Spec` from the command plus those values and keeps the command pure endpoint input. Event-driven reactions are satisfied by the domain event itself. Read-side `I*State` rehydration interfaces stay in `Domain` (implemented by Application persistence entities).

## Adding a new feature (vertical slice)

Follow this order:

1. **Domain** — aggregate method or domain service
2. **Application** — `{Feature}/` folder with command, handler, validator
3. **Infrastructure** — persistence if needed
4. **Endpoints** — one endpoint file, e.g. `GreetingEndpoints.cs`
5. **Tests** — domain unit tests and/or API integration tests for the new behavior (required when applicable; see [Testing — when finishing a task](./testing.md#when-finishing-a-task))

```
Modules/Acme.Modules.Greetings.Application/Create/
├── CreateGreetingCommand.cs
├── CreateGreetingHandler.cs
└── CreateGreetingValidator.cs
```

## Dependency injection

Each module registers itself in the API host:

```csharp
builder.Services.AddGreetingsModule(builder.Configuration);
builder.Services.AddWidgetsModule(builder.Configuration);
builder.Services.AddCqrsHandlersFrom(GreetingsApplicationMarker.Assembly);
builder.Services.AddCqrsHandlersFrom(WidgetsApplicationMarker.Assembly);
```

`AddCqrs(...)` registers `IRequestDispatcher` and auto-discovers `ICommandHandler<,>`, `IQueryHandler<,>`, and `IRequestValidator<>` implementations in the given assembly.

## API endpoints, OpenAPI & the generated client

- **Endpoints map the handler's `Result` with `result.ToHttpResult(onSuccess)`** (see [ADR-0013](../adr/0013-result-objects-cqrs.md)). Prefer strongly-typed `Results<Ok<T>, NotFound<ProblemDetails>, Conflict<ProblemDetails>, UnprocessableEntity<ProblemDetails>>` unions so OpenAPI infers the responses.
- **While an endpoint returns a bare `IResult`, OpenAPI can't infer its responses — annotate explicitly:** `.Produces<T>(StatusCodes.Status200OK)` (or `Status201Created`), `.ProducesValidationProblem()` for inline 400s, and `.ProducesDomainProblems()` for the shared 404/409/422 bodies. Once an endpoint returns a typed `Results<…>` union those success/error `.Produces*` calls fall away — the union supplies the metadata. Always keep `.WithName(nameof(Handler))` so the generated client gets a clean method name.
- **The OpenAPI document and the C# client are build artifacts, not committed.** The C# DTOs and endpoints are the single source of truth:
  - `dotnet build` exports `src/Services/Acme.Api/openapi/v1.json` (via `Microsoft.Extensions.ApiDescription.Server`, `--file-name v1`). The file is **git-ignored**, regenerated every build, never hand-edited. Generation runs in-memory — no database needed.
  - `src/Misc/Acme.ApiClient` generates a strongly-typed client (`AcmeApiClient`, NSwag) from that spec for the API tests and any other consumer. Generated code lives in `obj/` (git-ignored); it builds after the API via a build-order-only project reference.
- **After changing a DTO or endpoint:** just rebuild — the spec and C# client regenerate. The frontend TypeScript client regenerates separately.

See [ADR-0010](../adr/0010-openapi-contract-generated-clients.md) for the full rationale.

### Vertical slice pattern

A command/query marker's generic is its **success value**; handlers and the dispatcher return `Result<T>`. Use `ICommand<T>` for writes, `IQuery<T>` for reads (`IQueryHandler<,>`).

```csharp
// CreateGreetingHandler.cs — command + result + handler in one slice file
public sealed record CreateGreetingCommand(string Message)
    : ICommand<CreateGreetingResult>, ICreateGreetingSpec;

public sealed class CreateGreetingHandler : ICommandHandler<CreateGreetingCommand, CreateGreetingResult>
{
    public async Task<Result<CreateGreetingResult>> HandleAsync(
        CreateGreetingCommand command,
        CancellationToken cancellationToken)
    {
        // ... return Result.Success(new CreateGreetingResult { ... });
    }
}
```

Endpoint — map the `Result` to typed results by category (no exceptions for expected failures):

```csharp
app.MapPost("/api/greetings", async (CreateGreetingCommand cmd, IRequestDispatcher dispatcher, CancellationToken ct) =>
{
    var result = await dispatcher.SendAsync(cmd, ct);
    return result.ToHttpResult(value => TypedResults.Created($"/api/greetings/{value.GreetingId}", value));
});
```

A validation failure short-circuits in the dispatcher as a failed `Result` (category `Validation` → 422) — it is **not** thrown (see Error handling below).

## Persistence (PostgreSQL)

See [ADR-0009: Persistence patterns](../adr/0009-session-persistence-patterns.md) and [ADR-0016](../adr/0016-aggregate-decomposition-events-per-module-persistence.md) for full rationale.

### Read vs write

| Handler type | Inject | Purpose |
|--------------|--------|---------|
| **GET** (e.g. `GetGreetingHandler`) | the module's read port (e.g. `IGreetingReader`) | Read-only projections, no-tracking read contexts |
| **Commands** (POST/PATCH) | the module's aggregate repo (e.g. `IGreetingRepository`/`IWidgetRepository`) + `IUnitOfWork` | Stage changes, then commit |

### Adding a read query

A read query is a one-file change in the owning module's Application layer ([ADR-0015](../adr/0015-read-data-context.md)). The read port composes its view via the module's `IReadDataContext` using **`System.Linq` only** — `read.Query<TEntity>().Where(…).Select(…)` then a terminal (`FirstOrDefaultAsync`/`ToListAsync`/`AnyAsync`) on the context. **No `.Include`** — child collections are projected inside a nested `.Select` over the navigation property (EF translates it), then the view is built in memory. So **the Application layer never references EF Core** (enforced by an architecture test):

```csharp
internal sealed class GetGreetingHandler(IGreetingReader reader)
    : IQueryHandler<GetGreetingQuery, GreetingDto>
{
    public async Task<Result<GreetingDto>> HandleAsync(GetGreetingQuery query, CancellationToken ct)
    {
        var dto = await reader.GetAsync(query.GreetingId, ct);
        return dto is null
            ? Result<GreetingDto>.Failure(GreetingErrors.NotFound(query.GreetingId))
            : Result.Success(dto);
    }
}
```

The `IReadDataContext` is implemented twice per module in Infrastructure: an `Ef<M>ReadContext` over the no-tracking `<M>ReadDbContext`, and an `InMemory<M>ReadContext` over the in-memory store (both produce identical views). DI resolves a per-module marker (`IGreetingsReadContext`/`IWidgetsReadContext`) so each module gets its own read context. Cross-module reads never touch another module's internals — they go through a kernel read port by **typed id**.

### Command handler pattern

```csharp
internal sealed class AdjustQuantityHandler(
    IWidgetRepository repository,
    IUnitOfWork unitOfWork,
    IWidgetReader reader) : ICommandHandler<AdjustQuantityCommand, WidgetDto>
{
    public async Task<Result<WidgetDto>> HandleAsync(
        AdjustQuantityCommand command,
        CancellationToken cancellationToken)
    {
        var widget = await repository.GetByIdAsync(command.WidgetId, cancellationToken);
        if (widget is null)
            return Result<WidgetDto>.Failure(WidgetErrors.NotFound(command.WidgetId));

        var adjusted = widget.AdjustQuantity(command.Delta);
        if (adjusted.IsFailure)
            return Result<WidgetDto>.Failure(adjusted.Errors);   // propagate domain error

        repository.Update(adjusted.Value);                 // sync — stages only
        await unitOfWork.SaveChangesAsync(cancellationToken);  // async — dispatches events + commits

        var dto = await reader.GetAsync(command.WidgetId, cancellationToken);  // post-commit view
        return Result.Success(dto!);
    }
}
```

- Missing entity → `WidgetErrors.NotFound(...)` (category `NotFound`). Domain rule failures come back from the aggregate as a failed `Result` — propagate `outcome.Errors`.
- Reserve exceptions for **bugs and infrastructure** (broken invariants, DB faults), never for expected outcomes.
- `Add` / `Update` are **synchronous** — they queue pending changes on the scoped repository.
- `SaveChangesAsync` (the coordinating `AcmeUnitOfWork`) **dispatches domain events, then commits every write context in one transaction** — see [Domain events & cross-aggregate reactions](#domain-events--cross-aggregate-reactions) below.
- Any external call should finish **before** `SaveChangesAsync` so the DB transaction stays short.
- Build the command's response from the no-tracking read view **after** `SaveChangesAsync` commits, so it observes any reactions' effects without a manual reload.

## Domain events & cross-aggregate reactions

State that one capability owns but another's command must change is **not** poked directly across aggregates — the acting aggregate raises a **domain event** and the owning module reacts. See [ADR-0016](../adr/0016-aggregate-decomposition-events-per-module-persistence.md).

- **Raise:** an `AggregateRoot` calls `RaiseEvent(new GreetingCreated(...))`. Cross-module events live in the shared kernel (`Kernel.Domain.DomainEvents`) so any module can handle them; the primitives (`IDomainEvent`, `AggregateRoot`, `IDomainEventHandler<T>`, `IDomainEventDispatcher`, `TrackedAggregates`, `DomainEventDrain`) are in `CQRS.Abstractions`.
- **Handle:** implement `IDomainEventHandler<TEvent>` (internal, in the reacting module's Application layer); it loads/mutates its own aggregate and stages the write. Handlers **must not** depend on the unit of work (architecture-test enforced) — the UoW owns the commit.
- **Dispatch:** the unit of work drains tracked aggregates' events **before commit**, so a command and its reactions commit in **one transaction**. `DomainEventDrain` loops until no new events remain, so a reaction can raise its own event — all in the same request.
- **Read across modules** through a kernel **read port**, not another module's internals (by typed id only).

> In-process + transactional only — by design. Integration events / an outbox (cross-process, eventual consistency) are **out of scope**; the single coordinating transaction keeps everything consistent.

## Per-module persistence

Each **persistent** module owns a write `<M>DbContext` and a sibling no-tracking `<M>ReadDbContext` (same mapping via a shared `<M>DbContextBase`), its **own PostgreSQL schema**, and its own migration history ([ADR-0016](../adr/0016-aggregate-decomposition-events-per-module-persistence.md)): Greetings → `GreetingsDbContext`/`GreetingsReadDbContext` / `greetings`, Widgets → `WidgetsDbContext`/`WidgetsReadDbContext` / `widgets`. No context maps another module's tables.

A command and its event reactions can span more than one context, so the **coordinating `AcmeUnitOfWork`** keeps them atomic: the write contexts share one scoped connection; the UoW opens a single transaction, enlists each `ITransactionalParticipant` (one thin adapter per write context, registered by its module), dispatches events, saves all participants, and commits — rolling back everything on any failure.

**Adding a migration for a module** (run from the repo root, against that module's context):

```bash
dotnet ef migrations add <Name> \
  --project src/Modules/Acme.Modules.<M>.Infrastructure \
  --startup-project src/Modules/Acme.Modules.<M>.Infrastructure \
  --context <M>DbContext --output-dir Persistence/Migrations
```

Each context migrates on startup. EF migrations are CSharpier-ignored.

### API `Program.cs`

The host wires **no `DbContext`** — each module registers its own contexts in its `Add<M>Module`. When not using in-memory persistence, every write context shares one scoped `NpgsqlConnection` so the coordinating unit of work can commit them in a single transaction; each read context keeps its own. Because each write context is also registered as `DbContext`, the host migrates every schema generically without naming any context:

```csharp
// Modules register their own contexts:
builder.Services.AddGreetingsModule(builder.Configuration);
builder.Services.AddWidgetsModule(builder.Configuration);

// After app.Build(), when Postgres is enabled — migrate every registered write context:
using var scope = app.Services.CreateScope();
foreach (var context in scope.ServiceProvider.GetServices<DbContext>())
{
    context.Database.Migrate();
}
```

The write contexts are registered directly (not via Aspire's enriched `AddNpgsqlDbContext`) because a user-initiated transaction is incompatible with a retrying execution strategy. Connection strings are injected by Aspire (`ConnectionStrings:acme`).

### Infrastructure registration

Each module's `Add<M>Module(configuration)` owns its persistence wiring (e.g. `AddGreetingsModule`/`AddWidgetsModule`); `Acme.Kernel.Infrastructure/DependencyInjection.cs` adds the shared `NpgsqlConnection` + the coordinating `AcmeUnitOfWork`:

- **Read port** (e.g. `IGreetingReader`): registered **unconditionally** — it lives in Application and depends only on the module's `IReadDataContext`, so it is the same in both modes. Only the `IReadDataContext` implementation differs per mode.
- **Production:** per module — the write `<M>DbContext` (also registered as `DbContext`) on the shared connection, the no-tracking `<M>ReadDbContext` on its own connection, an `Ef…Repository` write repo, an `Ef<M>ReadContext` (`IReadDataContext`) over the read context, and one `…TransactionalParticipant` (`ITransactionalParticipant`) per write context.
- **Tests:** `Persistence:UseInMemory=true` → per module a singleton `InMemory…Store` (holds the **persistence entity**), a scoped `InMemory…Repository` + `InMemory<M>ReadContext` (`IReadDataContext`), and the in-memory UoW. The store rehydrates a fresh aggregate per read, so it matches EF's identity-per-load behavior.

### Application / kernel interfaces

| Interface | Owner | Methods |
|-----------|-------|---------|
| `IGreetingRepository` / `IWidgetRepository` | the module (Application) | `GetByIdAsync`, `Add`, `Update` |
| `IGreetingReader` / `IWidgetReader` | the module (Application) | `GetAsync`/`ListAsync` → DTO/view |
| `IReadDataContext` | Kernel | `Query<T>`, `ToListAsync`, `FirstOrDefaultAsync`, `AnyAsync` |
| `INotificationPublisher` | Kernel | `PublishAsync(NotificationMessage, ct)` (realtime) |
| `IUnitOfWork` | Kernel | `SaveChangesAsync` (dispatches events + commits all write contexts) |
| `ITransactionalParticipant` | Building blocks | `EnlistAsync`, `SaveChangesAsync` (one per write context) |

### Realtime notifications

A module pushes a realtime update by publishing a `NotificationMessage(channel, message)` through the kernel `INotificationPublisher` port. The API host binds that port to SignalR (the generic `NotificationsHub`); the web client subscribes to the hub and renders incoming messages (e.g. a toast). The Application layer depends only on `INotificationPublisher`, never on SignalR/ASP.NET (a non-HTTP host can swap in a no-op or test double). See [ADR-0003](../adr/0003-internal-api-via-ssr.md).

## Error handling

Expected failures travel as a failed `Result`; the endpoint maps `Error.Category` to `ProblemDetails`/`ValidationProblemDetails` via `result.ToHttpResult(...)`:

| `ErrorCategory` | HTTP | Example |
|-----------------|------|---------|
| `NotFound` | `404` | greeting/widget not found |
| `Conflict` | `409` | rule violation, invalid state |
| `Validation` | `422` | request validation failed (field map) |

Only genuine bugs and infrastructure faults throw; the global handler turns those into `500`. See [ADR-0013](../adr/0013-result-objects-cqrs.md).

## Testing backend changes

Tests are **required when finishing a task** if the change affects behavior. See [Testing — when finishing a task](./testing.md#when-finishing-a-task).

1. **Domain** — unit tests in the module domain test project
2. **API** — integration tests in `Acme.Api.Tests` with `TestWebApplicationFactory` (`Persistence:UseInMemory=true`)
3. **EF pipeline** — per-module persistence tests plus a coordinating-unit-of-work test (atomic commit + rollback across the per-module write contexts) with Testcontainers Postgres
4. **Verify** — `dotnet test --solution Acme.slnx` passes before marking the task done

See [Testing](./testing.md) for structure, fakes, and analyzer rules.

## Related ADRs

- [ADR-0002: Clean Architecture + vertical slices](../adr/0002-clean-architecture-vertical-slices.md)
- [ADR-0013: Result objects + CQRS split](../adr/0013-result-objects-cqrs.md) (supersedes [ADR-0008](../adr/0008-custom-request-dispatch.md))
- [ADR-0005: PostgreSQL via Aspire](../adr/0005-postgresql-via-aspire.md)
- [ADR-0009: Persistence patterns](../adr/0009-session-persistence-patterns.md) (updated by ADR-0016)
- [ADR-0016: Per-aggregate, per-module persistence + domain events](../adr/0016-aggregate-decomposition-events-per-module-persistence.md) (refines/extends [ADR-0015](../adr/0015-read-data-context.md): the `IReadDataContext` abstraction is split per module)
- [ADR-0017: Immutable Domain enforced by a Roslyn analyzer](../adr/0017-immutable-domain-enforced-by-analyzer.md) (no public `init`; construction via non-public ctor + factory)
- [ADR-0018: Domain spec interfaces + Domain.Contracts/Application.Contracts](../adr/0018-domain-spec-interfaces-contract-layering.md) (multi-parameter domain methods take a single `I…Spec`)
