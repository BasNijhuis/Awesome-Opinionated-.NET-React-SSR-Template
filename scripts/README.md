# `scripts` — dev/build scripts (workspace member)

Dev- and build-time helpers, kept in one pnpm-workspace member. **Not** part of the app runtime.
Run from the repo root via `pnpm --filter scripts run <name>`.

| Script | Command | What it does |
|--------|---------|--------------|
| `gen-contract` | `pnpm --filter scripts run gen-contract` | Regenerate the web client's contract types (invoked by the web `api:generate`). |

Add future repo tooling (data generators, codegen, migration helpers, …) here as additional scripts
rather than scattering them across the app packages.
