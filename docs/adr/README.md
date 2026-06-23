# Architecture Decision Records (ADRs)

This folder records the significant architectural decisions baked into this template. ADRs are immutable once accepted; superseded decisions get a new ADR with a link to the prior one.

## Index

| ADR | Title | Status |
|-----|-------|--------|
| [0001](./0001-aspire-orchestration.md) | Use .NET Aspire for local orchestration | Accepted |
| [0002](./0002-clean-architecture-vertical-slices.md) | Backend: DDD + Clean Architecture + vertical slices | Accepted |
| [0003](./0003-internal-api-via-ssr.md) | Internal-only API; browser talks to SSR server only | Accepted |
| [0004](./0004-react-router-ssr-pnpm.md) | React Router Framework SSR with pnpm | Accepted |
| [0005](./0005-postgresql-via-aspire.md) | PostgreSQL via Aspire container + data volume | Accepted |
| [0006](./0006-xunit-v3-mtp-v2.md) | xUnit v3 + Microsoft Testing Platform v2 | Accepted |
| [0007](./0007-slnx-solution-format.md) | XML solution file (`.slnx`) | Accepted |
| [0008](./0008-custom-request-dispatch.md) | Custom request dispatch + validation (no MediatR) | Superseded by [0013](./0013-result-objects-cqrs.md) |
| [0009](./0009-session-persistence-patterns.md) | Persistence: read/write split, unit of work, staged writes | Accepted · updated by [0016](./0016-aggregate-decomposition-events-per-module-persistence.md) |
| [0010](./0010-openapi-contract-generated-clients.md) | API contract via build-time OpenAPI and generated clients | Accepted |
| [0011](./0011-build-hygiene-formatting-lockfiles.md) | Build hygiene: CSharpier, NuGet lock files, solution-wide TFM | Accepted |
| [0012](./0012-biome-frontend-lint-format.md) | Frontend linting & formatting with Biome | Accepted |
| [0013](./0013-result-objects-cqrs.md) | Result objects for expected failures + CQRS split | Accepted |
| [0014](./0014-modular-monolith.md) | Modular monolith with enforced boundaries | Accepted · amended by [0016](./0016-aggregate-decomposition-events-per-module-persistence.md), [0018](./0018-domain-spec-interfaces-contract-layering.md) |
| [0015](./0015-read-data-context.md) | Application-composed read projections via `IReadDataContext` | Accepted |
| [0016](./0016-aggregate-decomposition-events-per-module-persistence.md) | Per-aggregate, per-module persistence + domain events | Accepted |
| [0017](./0017-immutable-domain-enforced-by-analyzer.md) | Immutable Domain layer enforced by a Roslyn analyzer | Accepted |
| [0018](./0018-domain-spec-interfaces-contract-layering.md) | Domain methods consume spec interfaces; Domain.Contracts vs Application.Contracts | Accepted |

## Template

When adding a new ADR, copy this structure:

```markdown
# ADR-NNNN: Title

- **Status:** Proposed | Accepted | Superseded by [NNNN](./NNNN-....md)
- **Date:** YYYY-MM-DD

## Context
## Decision
## Consequences
```

## Related docs

- [Instructions](../instructions/README.md) — day-to-day how-to guides
