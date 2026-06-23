# ADR-0018: Domain methods consume spec interfaces; Domain.Contracts vs Application.Contracts

- **Status:** Accepted
- **Date:** 2026-06-22
- **Amends:** [ADR-0014](./0014-modular-monolith.md) (module project layout + boundary rule for the contract layer)
- **Builds on:** [ADR-0017](./0017-immutable-domain-enforced-by-analyzer.md) (immutable domain, factory construction)

## Context

Public domain methods took loose positional parameter lists (e.g. `Widget.Rename(string, int)`). Multi-parameter signatures are easy to transpose and drift from the calling command. We already use **state-spec interfaces** for the read/rehydration side (`I*State` implemented by Application persistence entities); this applies the same idea to the **input** side.

Letting the command models implement domain spec interfaces created a wrinkle: the command/transport contracts (`*.Contracts`) would depend on the module `*.Domain`. In clean-architecture terms an outer layer depending inward on the domain is allowed, but we want the contract layer to see only the **abstract input surface**, not the domain implementation.

## Decision

### 1. Domain methods consume a single spec interface

A public domain method with **more than one data parameter** takes a single `I…Spec` interface (e.g. `ICreateGreetingSpec`, `IUpdateWidgetSpec`). Injected domain **services** and any shared snapshot stay as separate parameters — only data inputs go in the spec. Methods bind locals (`var message = spec.Message;`) so bodies stay unchanged.

### 2. Input specs live in `*.Domain.Contracts`

Each module that has command-driven specs gets a new **`Acme.Modules.<M>.Domain.Contracts`** project holding the `I…Spec` interfaces; it references **only `Kernel.Domain`** (the typed-id/enum value objects). So the contract layer depends on this thin abstract surface, never the domain implementation.

- **Event-implemented specs stay in the kernel:** a spec implemented by a kernel **domain event** (not by a command model) lives in `Kernel.Domain` beside that event (no module → kernel inversion).
- **Read-side `I*State` interfaces stay in `*.Domain`.** They expose concrete domain types, so they cannot move without dragging the domain in — and they're implemented by Application entities, where depending on `Domain` is normal.

### 3. The contract layer is `*.Application.Contracts`

The module `*.Contracts` projects are renamed **`*.Application.Contracts`** — CQRS commands/queries and their result DTOs are an application concern. When every spec member is endpoint-provided, the command model implements its spec directly (`CreateGreetingCommand : ICommand<…>, ICreateGreetingSpec`). When a method also needs **handler-derived** inputs (a generated id, a default value, a value read from another aggregate), the handler composes a `private sealed record` implementing the spec from the command plus those values — the command stays pure endpoint input. `Kernel.Contracts` (shared cross-module DTOs/read views) is unchanged.

### 4. Enforcement

A new architecture test asserts **`*.Application.Contracts` must not reference the module `*.Domain` assembly** (it may reference `*.Domain.Contracts`). It's checked at the **assembly-reference** level via `Assembly.GetReferencedAssemblies()`, because `Acme.Modules.<M>.Domain` is a namespace prefix of `…Domain.Contracts`, so a namespace-based `NotHaveDependencyOn` can't separate them. The two pre-existing "contracts don't depend on internals/application" rules moved to the same assembly-reference form for the same reason (`…Application.Contracts` is a prefix-child of `…Application`).

## Consequences

### Positive
- The command/transport layer depends only on the domain's abstract input ports (`Domain.Contracts`), not its implementation; the separation is build-enforced.
- Inputs are named and explicit; a command flows straight into the aggregate with no positional re-mapping.
- Symmetric with the read-side `I*State` spec pattern.

### Negative
- More projects: a `Domain.Contracts` per module that has command-driven specs.
- `Application.Contracts` nests under the `.Application` namespace, so module-isolation/contract arch rules are assembly-reference based rather than namespace based.
- A method needing handler-derived inputs is constructed via a private handler-side spec record rather than the command implementing the spec — the command stays pure endpoint input.
- Internal-only factories and event factories keep positional construction (out of scope).

## Related
- [ADR-0014: Modular monolith](./0014-modular-monolith.md) (amended — the module now has `Domain.Contracts` and `Application.Contracts`; the contract layer references `Domain.Contracts`, not the module `Domain`)
- [ADR-0017: Immutable Domain enforced by an analyzer](./0017-immutable-domain-enforced-by-analyzer.md) (construction via factory + non-public ctor)
- [project-structure.md](../instructions/project-structure.md) · [backend-development.md](../instructions/backend-development.md)
