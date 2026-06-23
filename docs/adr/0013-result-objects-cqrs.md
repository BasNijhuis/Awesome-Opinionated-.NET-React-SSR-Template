# ADR-0013: Result objects for expected failures + CQRS split

- **Status:** Accepted
- **Date:** 2026-06-21
- **Supersedes:** [ADR-0008](./0008-custom-request-dispatch.md)

## Context

The original request pipeline ([ADR-0008](./0008-custom-request-dispatch.md)) routed `IRequest<T>` to `IRequestHandler<T>` and signalled **every** expected failure — entity not found, wrong state, validation — by throwing (`NotFoundException`, `InvalidStateException`, `ValidationException`). A global exception-handler middleware turned those into `ProblemDetails`.

Using exceptions for expected, recoverable outcomes has costs: control flow is invisible at the call site, the handler signature lies (it "returns" a value it may never produce), and tests must assert on thrown types. We decided to switch expected failures to **result objects** and to adopt a fluent assertion library.

## Decision

### 1. `Result` / `Result<T>` for expected failures

- `Result` and `Result<T>` carry success/failure plus one or more `Error`s. `Error` has a `Code`, `Message`, and an `ErrorCategory` (`NotFound` | `Conflict` | `Validation`).
- **Expected, recoverable failures return a failed `Result`.** Domain rule violations, missing entities, and validation are values, not exceptions.
- **Bugs, broken invariants, and infrastructure faults still throw.** A lookup that misses after the caller already checked existence is a programmer error (`InvalidOperationException`); a dropped DB connection is infrastructure.
- Error definitions are **centralised** per module so codes/messages stay consistent (e.g. `GreetingErrors`, `WidgetErrors`).

### 2. CQRS command/query split

| Concern | Type | Role |
|---------|------|------|
| Read marker | `IQuery<TResult>` | `TResult` is the success value |
| Write marker | `ICommand<TResult>` | `TResult` is the success value |
| Read handler | `IQueryHandler<TQuery, TResult>` | returns `Task<Result<TResult>>` |
| Write handler | `ICommandHandler<TCommand, TResult>` | returns `Task<Result<TResult>>` |
| Dispatch | `IRequestDispatcher.QueryAsync` / `SendAsync` | returns `Task<Result<TResult>>` |
| Validation | `IRequestValidator<T>` / `RequestValidator<T>` | runs before the handler; a failure becomes a **failed `Result`**, not a thrown `ValidationException` |

The dispatcher runs validators first; if any fail it short-circuits with `Result<TResult>.Failure(validationErrors)`. The marker's generic argument is the *success value*, which lets the dispatcher construct the correctly-typed failure without reflection on the result.

### 3. Project layout (dependency direction)

The result and CQRS abstractions live in dedicated projects so **Domain does not depend on CQRS**:

```
Acme.DomainAbstractions          Result, Result<T>, Error, ErrorCategory   (no dependencies)
Acme.CQRS.Abstractions           IQuery/ICommand, handlers, IRequestDispatcher, validation  → DomainAbstractions
Acme.CQRS                        RequestDispatcher + AddCqrs(...) DI       → CQRS.Abstractions
Acme.Modules.<M>.Domain          aggregates, domain services              → DomainAbstractions
Acme.Modules.<M>.Application     handlers, validators                     → Domain, CQRS
```

`services.AddCqrs(assembly)` registers the dispatcher and scans an assembly for handlers/validators; `AddApplication()` calls it for the Application assembly.

### 4. API mapping (contract unchanged)

Endpoints translate a `Result` to `TypedResults` by category — `NotFound` → 404, `Conflict` → 409, `Validation` → 422 — preserving the existing `ProblemDetails` / `ValidationProblemDetails` shapes. The OpenAPI document and generated clients are **byte-for-byte unchanged**.

### 5. Tests

- **AwesomeAssertions** (`.Should()`) replaces raw `Assert.*`.
- **Arrange–Act–Assert with a single action in Act.** Setup steps (including multi-step state changes) belong in Arrange; assertions on the result belong in Assert.
- Domain test Arrange builds state through a **builder** (e.g. `GreetingBuilder`) rather than calling aggregate methods inline.

## Consequences

### Positive

- Expected failures are explicit in handler/aggregate signatures and at call sites.
- No exceptions for control flow; the global handler now only catches genuine faults (→ 500).
- Clean dependency graph: Domain has no CQRS knowledge; result primitives are shared by both.
- Tests read as data assertions (`result.IsFailure`, `result.Error.Category`) instead of `Assert.Throws`.

### Negative

- More plumbing: handlers thread `Result` through and propagate `outcome.Errors`.
- The dispatcher still uses reflection to invoke `HandleAsync` (unchanged from ADR-0008).
- Two extra projects to maintain.

## Related

- [ADR-0008: Custom request dispatch](./0008-custom-request-dispatch.md) (superseded)
- [ADR-0002: Clean Architecture + vertical slices](./0002-clean-architecture-vertical-slices.md)
- [ADR-0010: OpenAPI contract & generated clients](./0010-openapi-contract-generated-clients.md)
- [backend-development.md](../instructions/backend-development.md) · [testing.md](../instructions/testing.md)
