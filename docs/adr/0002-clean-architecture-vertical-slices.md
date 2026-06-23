# ADR-0002: Backend — DDD + Clean Architecture + vertical slices

- **Status:** Accepted (amended by [ADR-0014](./0014-modular-monolith.md))
- **Date:** 2026-06-19

> **Amended by [ADR-0014](./0014-modular-monolith.md):** the layers + vertical slices below now live
> *inside capability modules* (Greetings/Widgets in this template), each a set of layer projects with a
> public `.Contracts`, over a shared kernel — with NetArchTest-enforced boundaries.

## Context

Business rules (the example here: a `Greeting`'s message, a `Widget`'s name and quantity) must live in testable domain code—not in controllers or EF entities. We also want features added incrementally without scattering logic across unrelated service classes.

## Decision

Organize the backend into **Clean Architecture** layers with **vertical slices** in the Application/API layers:

```
Acme.Api              ← thin endpoints, one slice per use case
Acme.Application      ← commands/queries/handlers (vertical slices)
Acme.Domain           ← aggregates, value objects, domain services
Acme.Infrastructure   ← EF Core, repositories, seed data
```

**Dependency rule:** Domain has zero outward references. Application depends on Domain only. Infrastructure implements Application/Domain abstractions. Api references Application + Infrastructure.

**Vertical slices** (one folder per use case under `Application/`):

```
Greetings/
├── Create/
├── GetById/
└── ...
Widgets/
├── Create/
├── AdjustQuantity/
└── ...
```

Each API endpoint maps to exactly one handler. Domain invariants are enforced in aggregates, not in the UI.

## Consequences

### Positive

- Business rules are unit-testable without a database.
- New features add a folder, not a cross-cutting change.
- Clear onboarding: "find the slice, read the handler."

### Negative

- More projects than a minimal API template.
- Requires discipline to keep Api thin and avoid anemic domain models.
- Custom request dispatch/validation is maintained in-house—see [ADR-0008](./0008-custom-request-dispatch.md).

### Current state

Layers are implemented with vertical slices in each module's Application layer. Persistence uses PostgreSQL (Aspire) with read/write DbContexts, staged repository writes, and `IUnitOfWork.SaveChangesAsync` — see [ADR-0009](./0009-session-persistence-patterns.md).
