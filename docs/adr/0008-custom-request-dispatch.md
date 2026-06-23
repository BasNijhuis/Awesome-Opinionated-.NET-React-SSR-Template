# ADR-0008: Custom request dispatch and validation (no MediatR / FluentValidation)

- **Status:** Superseded by [ADR-0013](./0013-result-objects-cqrs.md)
- **Date:** 2026-06-19

> **Superseded:** the in-house dispatch decision still holds, but the specific shape changed in
> [ADR-0013](./0013-result-objects-cqrs.md): `IRequest<T>` split into `IQuery<T>`/`ICommand<T>`,
> handlers and the dispatcher now return `Result<T>`, validation failures return a failed `Result`
> instead of throwing, and the abstractions moved into the `Acme.CQRS(.Abstractions)`
> projects. The sections below reflect the original design.

## Context

Vertical slices need a way to route commands/queries to handlers and validate input before domain logic runs. MediatR and FluentValidation are common choices but add external dependencies, assembly scanning conventions, and pipeline behaviors we do not need yet for a focused API.

## Decision

Implement a **minimal in-house request pipeline** in `Acme.Application`:

| Concern | Type | Role |
|---------|------|------|
| Request marker | `IRequest<TResponse>` | Command/query DTO |
| Handler | `IRequestHandler<TRequest, TResponse>` | One use case per class |
| Dispatch | `IRequestDispatcher.SendAsync` | Resolves handler, runs validators, invokes |
| Validation | `IRequestValidator<TRequest>` / `RequestValidator<T>` | Per-request rules; throws `ValidationException` |

Registration: `services.AddApplication()` scans the Application assembly for handlers and validators.

Endpoints call `IRequestDispatcher` directly—no mediator package, no FluentValidation dependency.

## Consequences

### Positive

- Zero extra NuGet packages for dispatch/validation.
- Full control over behavior and error types.
- Same vertical-slice folder layout as planned (`Command`, `Handler`, `Validator`).

### Negative

- We maintain the dispatcher ourselves (currently ~80 lines).
- No MediatR-style pipeline behaviors (logging, transactions)—add explicitly if needed later.

### Usage

```csharp
// Endpoint
app.MapPost("/api/greetings", async (CreateGreetingCommand cmd, IRequestDispatcher dispatcher, CancellationToken ct) =>
{
    var result = await dispatcher.SendAsync(cmd, ct);
    return Results.Created($"/api/greetings/{result.GreetingId}", result);
});
```

## Related

- [ADR-0002: Clean Architecture + vertical slices](./0002-clean-architecture-vertical-slices.md)
