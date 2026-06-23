# ADR-0011: Build hygiene — CSharpier, NuGet lock files, solution-wide TFM

- **Status:** Accepted
- **Date:** 2026-06-20

## Context

As the solution grew (now 9 projects), small inconsistencies accumulated: per-project target frameworks repeated in every `.csproj`, no enforced code formatting, and no restore determinism. We want repo-wide consistency and reproducible builds without bespoke scripts.

(xUnit analyzers are **not** part of this decision — they arrive transitively via `xunit.v3.mtp-v2`, so no explicit reference is added; see [ADR-0006](./0006-xunit-v3-mtp-v2.md).)

## Decision

Centralize build conventions in `Directory.Build.props` and add standard tooling:

| Concern | Choice |
|---------|--------|
| **Target framework** | `<TargetFramework>net10.0</TargetFramework>` defined **once** in `Directory.Build.props`; removed from every project. |
| **Restore determinism** | `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>`; each project commits a `packages.lock.json`. |
| **Formatting** | **CSharpier** as a local dotnet tool (`.config/dotnet-tools.json`); `dotnet csharpier format .`. EF migrations excluded via `.csharpierignore`. |

Package versions remain centrally managed in `Directory.Packages.props` ([ADR-0008] context), and the solution stays in `.slnx` format ([ADR-0007](./0007-slnx-solution-format.md)).

## Consequences

### Positive

- One place to bump the TFM for the whole solution.
- Reproducible restores; lock files make dependency changes reviewable in diffs.
- Uniform formatting (`dotnet csharpier`) across the repo.

### Negative

- `packages.lock.json` files must be regenerated/committed when dependencies change (`dotnet restore`).
- A formatting tool to run/keep in CI (a fresh clone needs `dotnet tool restore` before `dotnet csharpier`).
- Generated/inherited files (EF migrations) need explicit ignore entries.

### Usage

```bash
dotnet tool restore        # once per clone — installs CSharpier from the manifest
dotnet csharpier format .  # format the repo (CI can use `dotnet csharpier check .`)
dotnet restore             # refresh packages.lock.json after dependency changes
```

## Related

- [ADR-0006](./0006-xunit-v3-mtp-v2.md) · [ADR-0007](./0007-slnx-solution-format.md)
- [instructions/local-development.md](../instructions/local-development.md)
