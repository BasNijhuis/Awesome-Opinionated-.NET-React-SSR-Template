# ADR-0014: Modular monolith with enforced boundaries

- **Status:** Accepted · amended by [ADR-0018](./0018-domain-spec-interfaces-contract-layering.md) (module now has `Domain.Contracts`; `Contracts` → `Application.Contracts`)
- **Date:** 2026-06-21
- **Amends:** [ADR-0002](./0002-clean-architecture-vertical-slices.md) (layers + vertical slices remain, now *within* modules)

## Context

The backend was organized by technical layer with vertical-slice features inside each layer ([ADR-0002](./0002-clean-architecture-vertical-slices.md)). Everything was one bounded context, and nothing stopped one feature from reaching into another's internals. We chose to organize the backend as a **modular monolith** around business capabilities and to **enforce** the module boundaries so violations fail the build.

## Decision

A **pragmatic modular monolith**: cross-cutting primitives stay in a **shared kernel**; each business capability becomes a **module**, split into clean-architecture layer projects with a published contract. The template ships two example modules — **Greetings** and **Widgets** — that demonstrate the structure end to end.

### Modules (capabilities)

`Greetings` (a `Greeting` aggregate: a persisted `Message`) and `Widgets` (a `Widget` aggregate: `Name` + `Quantity`). Each is a set of projects:

| Project | Visibility | Holds |
|---------|-----------|-------|
| `Acme.Modules.<M>.Application.Contracts` | **public** | commands/queries (`ICommand`/`IQuery`), result/event DTOs — the only surface other code may reference |
| `Acme.Modules.<M>.Domain.Contracts` | **public** | the domain's input **spec interfaces** (`I…Spec`); references only `Kernel.Domain` (see [ADR-0018](./0018-domain-spec-interfaces-contract-layering.md)) |
| `Acme.Modules.<M>.Domain` | internal | the module's aggregate + its domain rules |
| `Acme.Modules.<M>.Application` | internal | handlers, validators, ports, mappers (handlers are `internal`, discovered by reflection) |
| `Acme.Modules.<M>.Infrastructure` | internal | adapters + the module's `Add<M>Module()` DI registration |
| `Acme.Modules.<M>.Endpoints` | public | minimal-API `Map<M>Endpoints()` + request DTOs |

Compiler-enforced internal layering per module: **Domain ← Application ← Infrastructure**; Endpoints → Contracts only.

> The split of `Contracts` into `Application.Contracts` + `Domain.Contracts` was introduced by [ADR-0018](./0018-domain-spec-interfaces-contract-layering.md), which amends this ADR.

### Shared kernel & building blocks

- **Kernel** (`Acme.Kernel.*`): `Domain` (cross-cutting value objects, typed-id primitives, and the shared domain-event contract), `Application` (shared ports — `IUnitOfWork`, the realtime `INotificationPublisher`, `IReadDataContext` — plus hub topics), `Contracts` (shared cross-module DTOs such as `NotificationMessage`). `Result`/`Error` live in the `Acme.DomainAbstractions` building block.
- **Building blocks** (`Acme.{CQRS,CQRS.Abstractions,Http,DomainAbstractions,DomainAnalyzers}`): the dispatcher + `AddCqrs`/`AddCqrsHandlersFrom`, the CQRS/validation/domain-event abstractions, the shared `ToHttpResult`/problem mapping for endpoints, the `Result`/`Error` primitives, and the immutable-domain analyzer.

### Boundary rules

1. A module references **only** the shared kernel/building blocks + other modules' contract projects (`.Application.Contracts`/`.Domain.Contracts`) — never another module's `Domain`/`Application`/`Infrastructure`/`Endpoints`.
2. Cross-module reactions go through the dispatcher, an in-process **domain event**, or a kernel **port** — never a direct reference to another module's internals.
3. The kernel depends on **no** module. `Domain` depends only on `DomainAbstractions`.
4. `Application.Contracts` depends on `Domain.Contracts` and the shared `Kernel.Contracts` DTOs, never on any module's `Domain` or Application layer.

### Enforcement

`tests/Acme.Architecture.Tests` (NetArchTest.Rules) encodes the rules — explicit `OnlyHaveDependenciesOn` allow-lists for leaf projects, deny-lists for the cross-module/kernel rules, plus a "teeth" test proving the scan detects real references. Every project carries a uniform `public sealed class AssemblyMarker;` used both for the DI handler-scan and as a stable arch-test anchor.

### Composition & deployment

Single deployable (Aspire AppHost, one API process), single PostgreSQL database. The Api host composes modules: `Add<M>Module()` (services) + `AddCqrsHandlersFrom(<module>)` (handlers) and `Map<M>Endpoints()`. OpenAPI operation **tags** group operations by module. Realtime is a generic `NotificationsHub` (see [ADR-0003](./0003-internal-api-via-ssr.md)).

### Solution layout

`.slnx` groups projects into `Kernel`, `BuildingBlocks`, `Modules`, and `Aspire` solution folders; `Api`, the `ApiClient`, and the SSR frontend live under `src/Services` / `src/Misc`.

## Consequences

### Positive

- Capability boundaries are compile-time enforced; a module's internals are unreachable from other modules.
- Adding a capability is a well-defined recipe (a fixed set of projects + composition), not a cross-cutting edit.
- Clean dependency graph; the kernel has no module knowledge.

### Negative

- Many projects per module. Mitigated by the uniform structure and solution folders.
- The ceremony only pays off once a codebase has several capabilities; for a tiny app it is more structure than strictly needed.

## Related

- [ADR-0002: Clean Architecture + vertical slices](./0002-clean-architecture-vertical-slices.md) (amended)
- [ADR-0013: Result objects + CQRS split](./0013-result-objects-cqrs.md)
- [ADR-0016: Per-aggregate, per-module persistence + domain events](./0016-aggregate-decomposition-events-per-module-persistence.md)
- [ADR-0018: Domain spec interfaces + contract layering](./0018-domain-spec-interfaces-contract-layering.md)
- [backend-development.md](../instructions/backend-development.md) · [project-structure.md](../instructions/project-structure.md)
