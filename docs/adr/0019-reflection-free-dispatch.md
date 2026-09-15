# ADR-0019: Reflection-free request and domain-event dispatch

- **Status:** Accepted
- **Date:** 2026-09-15
- **Amends:** [ADR-0013](./0013-result-objects-cqrs.md)

## Context

[ADR-0013](./0013-result-objects-cqrs.md) listed as a standing negative:

> The dispatcher still uses reflection to invoke `HandleAsync` (unchanged from [ADR-0008](./0008-custom-request-dispatch.md)).

Three call sites were affected: `RequestDispatcher` invoked `HandleAsync` through
`MethodInfo.Invoke`, ran validators through a second `GetMethod("Validate")` + `Invoke`, and
`DomainEventDispatcher` invoked each `IDomainEventHandler<T>.HandleAsync` the same way.

All three shared one cause. The dispatcher knows the **success type** `TResult` statically — that is
what `ICommand<TResult>`/`IQuery<TResult>` buys — but it only learns the **request type** at runtime,
from `request.GetType()`. It therefore cannot name `ICommandHandler<TCommand, TResult>` in source and
fell back to closing the generic at runtime and invoking reflectively.

The costs: a handler whose signature drifts fails at runtime rather than at compile time (hence the
`handleMethod is null` and `is not Task<Result<TResult>>` guards, both unprovable by the compiler); a
throwing domain-event handler surfaced wrapped in `TargetInvocationException`; and the invoke path is
opaque to trimming and native AOT.

## Decision

Dispatch goes through **closed-generic adapters, registered in DI keyed by the runtime request or
event type**. The adapter is the single place where the request type *is* statically known, so it
calls the handler directly.

```csharp
internal interface IQueryHandlerAdapter<TResult>
{
    ImmutableArray<Error> Validate(object request);
    Task<Result<TResult>> HandleAsync(object request, CancellationToken cancellationToken);
}

internal sealed class QueryHandlerAdapter<TQuery, TResult>(
    IServiceProvider serviceProvider,
    IEnumerable<IRequestValidator<TQuery>> validators
) : IQueryHandlerAdapter<TResult>
    where TQuery : IQuery<TResult>
{
    public ImmutableArray<Error> Validate(object request) =>
        ValidationRunner.Run((TQuery)request, validators);

    public Task<Result<TResult>> HandleAsync(object request, CancellationToken cancellationToken) =>
        serviceProvider
            .GetRequiredService<IQueryHandler<TQuery, TResult>>()
            .HandleAsync((TQuery)request, cancellationToken);
}
```

The dispatcher resolves it and makes a plain interface call:

```csharp
var adapter = serviceProvider.GetRequiredKeyedService<IQueryHandlerAdapter<TResult>>(query.GetType());
```

The casts inside the adapter cannot fail: the adapter is only ever resolved under the key
`request.GetType()`, so the key *is* the proof that the cast holds.

The handler is **resolved on use rather than injected**. Injecting it would build the handler's whole
dependency graph — repository, `DbContext`, connection — while constructing the adapter, i.e. before
validation had a chance to reject the request. Resolving inside `HandleAsync` keeps the original
ordering, where an invalid request costs nothing beyond its validators. `GetRequiredService<T>` with a
static `T` is a dictionary lookup, not reflection.

There is a mirror pair for commands. **Domain events get one adapter per event type, not per
handler** — it takes `IEnumerable<IDomainEventHandler<TEvent>>` and fans out inside the typed layer,
so multiplicity and ordering are preserved when two modules handle the same kernel event.
Registration of that adapter is deduplicated across the whole `IServiceCollection` for exactly that
reason.

### Reflection at registration is retained, deliberately

`AddCqrs` / `AddCqrsHandlersFrom` still call `Assembly.GetTypes()` and now also `MakeGenericType` to
close each adapter. That is composition-root work, done once at startup, and is inherent to
convention-based handler discovery ([ADR-0014](./0014-modular-monolith.md)). The decision here is
narrow and specific: **no reflection on the dispatch path**. Removing it from registration too would
require a source generator — see *Not chosen*.

### Registering a single handler

A handler registered by hand gets no adapter and would be invisible to the dispatcher, so
`services.AddScoped<IQueryHandler<TQuery, TResult>, THandler>()` is no longer the way to register
one. Use the helpers, which are fully static (no `MakeGenericType` at all):

```csharp
services.AddQueryHandler<PingHandler, PingQuery, string>();
services.AddCommandHandler<CountHandler, CountCommand, int>();
services.AddDomainEventHandler<WidgetReactionHandler, GreetingCreated>();
```

### A handler without an adapter is a boot failure

A handler registered directly still resolves from DI, so nothing complains at registration. A command
or query would throw on first dispatch, but a **domain-event handler would be silently skipped** —
and per [ADR-0016](./0016-aggregate-decomposition-events-per-module-persistence.md) those reactions
run inside the caller's transaction, so a dropped one means a committed write whose side effect never
happened. Silence is the worst possible failure mode for that.

`services.ValidateCqrsRegistrations()` cross-checks every registered handler against the adapter keys
and throws if any is orphaned. The API host calls it once, after every module has registered and
before `builder.Build()`.

### Not chosen

- **A source generator** emitting a static request→handler switch. It would remove registration-time
  reflection as well, and is the natural next step if trimming or native AOT becomes a goal — but it
  is a much larger change for a cost that is paid once per process start.
- **Generic dispatch (`SendAsync<TCommand, TResult>`)**. C# cannot infer `TResult` from `TCommand`,
  so every endpoint would have to spell out both type arguments.
- **A non-keyed adapter list** filtered by a `RequestType` property. Works, but turns every dispatch
  into a linear scan of every registered handler.

## Consequences

### Positive

- No `MethodInfo.Invoke` on the dispatch path. The two runtime guards it needed (`handleMethod is
  null`, `is not Task<Result<TResult>>`) are gone — both are now compile-time impossibilities.
- A handler whose signature does not match its interface is a **compile error**, not a runtime one.
- A throwing domain-event handler propagates its **original** exception; no
  `TargetInvocationException` wrapper between the handler and the unit of work's rollback.
- Validators resolve as `IEnumerable<IRequestValidator<TRequest>>` — typed, and no longer reachable
  through the non-generic `IEnumerable` the reflective lookup required.
- Dispatch is a virtual call, so the `object[]` argument array that `MethodInfo.Invoke` required per request is gone.

### Negative

- Three adapter types plus a `ValidationRunner` to maintain — indirection whose purpose is not
  self-evident from the call site, hence the doc comments pointing here.
- Registering a handler outside an assembly scan now requires `AddCommandHandler` /
  `AddQueryHandler` / `AddDomainEventHandler`. A plain `AddScoped` of the handler interface still
  compiles but leaves the dispatcher unable to find it.
- Dispatching a request with **no** registered handler now throws when the adapter is resolved,
  before validators run. Previously validation ran first, so a request with a failing validator and
  no handler returned a validation `Result`. Both are misconfigurations; failing loudly is the
  better of the two, and the exception names the request type and the helper that fixes it.
- One more indirection between a stack trace and the handler: frames now pass through the adapter.

## Related

- [ADR-0013: Result objects for expected failures + CQRS split](./0013-result-objects-cqrs.md) — amended by this ADR
- [ADR-0008: Custom request dispatch](./0008-custom-request-dispatch.md) (superseded by 0013)
- [ADR-0014: Modular monolith](./0014-modular-monolith.md) — handlers are `internal`, discovered by scanning
- [backend-development.md](../instructions/backend-development.md)
