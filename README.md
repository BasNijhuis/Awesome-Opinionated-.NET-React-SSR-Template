# Awesome Opinionated Template

A batteries-included starter for building a **modular-monolith web application** on **.NET Aspire**
with a **server-rendered React frontend**. Instead of a blank `dotnet new`, you start from a coherent,
opinionated architecture: capability modules with enforced boundaries, an immutable domain enforced by
analyzers, CQRS with result objects, per-module PostgreSQL persistence, a build-time-generated API
contract with typed clients on both ends, and a React Router SSR frontend wired through Aspire service
discovery.

Every decision here is documented in an [ADR](docs/adr/README.md) and explained in the
[developer instructions](docs/instructions/README.md). This README is the map; those are the manual.

The template ships with two trivial example modules — **Greetings** and **Widgets** — that exercise the
whole stack end to end. Use them as the recipe for your own capabilities, then delete them.

---

## The stack

| Concern | Choice | ADR |
|---|---|---|
| Orchestration | .NET Aspire (one AppHost runs API + web + Postgres, with a dashboard) | [0001](docs/adr/0001-aspire-orchestration.md) |
| Backend style | DDD + Clean Architecture, vertical slices, **modular monolith** | [0002](docs/adr/0002-clean-architecture-vertical-slices.md), [0014](docs/adr/0014-modular-monolith.md) |
| API exposure | Internal-only API; the **browser only talks to the SSR server** | [0003](docs/adr/0003-internal-api-via-ssr.md) |
| Frontend | React Router v8 (framework SSR) + pnpm | [0004](docs/adr/0004-react-router-ssr-pnpm.md) |
| Database | PostgreSQL via Aspire container + data volume, EF Core | [0005](docs/adr/0005-postgresql-via-aspire.md) |
| Testing | xUnit v3 + Microsoft Testing Platform v2 | [0006](docs/adr/0006-xunit-v3-mtp-v2.md) |
| Solution format | `.slnx` (XML) | [0007](docs/adr/0007-slnx-solution-format.md) |
| Request handling | In-house CQRS dispatch + **`Result<T>`** for expected failures | [0008](docs/adr/0008-custom-request-dispatch.md), [0013](docs/adr/0013-result-objects-cqrs.md) |
| Persistence | Read/write split, coordinating unit of work, per-module schema | [0009](docs/adr/0009-session-persistence-patterns.md), [0016](docs/adr/0016-aggregate-decomposition-events-per-module-persistence.md) |
| API contract | Build-time OpenAPI → generated C# (NSwag) + TypeScript (hey-api) clients | [0010](docs/adr/0010-openapi-contract-generated-clients.md) |
| Build hygiene | CSharpier, NuGet lockfiles, solution-wide TFM | [0011](docs/adr/0011-build-hygiene-formatting-lockfiles.md) |
| Frontend lint/format | Biome | [0012](docs/adr/0012-biome-frontend-lint-format.md) |
| Read models | Application-composed projections over `IReadDataContext` | [0015](docs/adr/0015-read-data-context.md) |
| Domain integrity | **Immutable domain enforced by a Roslyn analyzer** | [0017](docs/adr/0017-immutable-domain-enforced-by-analyzer.md) |
| Domain inputs | Methods consume spec interfaces; `Domain.Contracts` vs `Application.Contracts` | [0018](docs/adr/0018-domain-spec-interfaces-contract-layering.md) |

---

## Getting started

### Prerequisites

- **.NET 10 SDK**
- **Docker Desktop** (for the PostgreSQL container)
- **Node.js 22+** and **pnpm 11+** (`corepack enable`)
- **Aspire CLI** (`dotnet tool install -g aspire.cli --version 13.4.5`)

### Run the whole stack

1. Start Docker Desktop.
2. From the repo root:

   ```bash
   aspire run
   # or, without the Aspire CLI:
   dotnet run --project src/Aspire/Acme.AppHost
   ```

3. Open the **acme-web** URL from the Aspire dashboard and try the **Greetings** and **Widgets** pages.

One command starts everything:

| Resource | What it is |
|---|---|
| `postgres` | PostgreSQL container with a persisted data volume (db: `acme`) |
| `acme-api` | The internal HTTP API — migrations run on startup; **no external endpoint** |
| `acme-web` | The React Router SSR app — **the only externally reachable surface** |

On first run the web app generates its typed API client (run `pnpm install` once at the repo root
beforehand); the API applies EF migrations for every module schema automatically. See
[local-development.md](docs/instructions/local-development.md) for standalone (non-Aspire) runs,
HTTPS certs, and troubleshooting.

### Build, test, format

```bash
dotnet build Acme.slnx                  # whole solution; analyzers run here
dotnet test --solution Acme.slnx        # xUnit v3 + MTP v2 (Docker needed for the Testcontainers tests)
dotnet csharpier format .               # C# formatting (CI uses `check`)

pnpm install                            # once, at the repo root (single pnpm workspace)
pnpm --filter acme-web run typecheck    # regenerates the API client, then tsc
pnpm run lint                           # Biome over all workspace members (web, scripts, tests/e2e)
```

---

## Architecture at a glance

### Modular monolith

The backend is one deployable composed of **capability modules** over a thin **shared kernel** and a set
of **building blocks**. Each module is a vertical stack of clean-architecture layer projects with a public
contract; the rest of the system may reference only that contract. Boundaries are **compile-enforced** by
project references and asserted by [NetArchTest architecture tests](tests/Acme.Architecture.Tests).

```
Browser ──public──▶ acme-web (React Router SSR)
                        │  internal (Aspire service discovery)
                        ▼
                    acme-api  ──▶  PostgreSQL (schema per module)
```

A single module is six projects (only `Domain*` are added when the module owns rules):

| Project | Visibility | Holds |
|---|---|---|
| `Acme.Modules.<M>.Domain` | internal | aggregate(s), value objects, state-spec interfaces — **immutable** |
| `Acme.Modules.<M>.Domain.Contracts` | public | typed id + input-spec interfaces the domain methods consume |
| `Acme.Modules.<M>.Application.Contracts` | public | CQRS commands/queries, result DTOs — the surface other code references |
| `Acme.Modules.<M>.Application` | internal | handlers, validators, persistence entities, read projections |
| `Acme.Modules.<M>.Infrastructure` | internal | EF `DbContext` + migrations + the `Add<M>Module()` DI registration |
| `Acme.Modules.<M>.Endpoints` | public | minimal-API `Map<M>Endpoints()` + request DTOs |

**Dependency rule:** Domain has zero outward references; Application depends on Domain; Infrastructure
implements Application/Domain abstractions; a module reaches other modules only through the shared kernel
or another module's `.Application.Contracts`. Adding a module is a documented recipe — see
[project-structure.md](docs/instructions/project-structure.md).

### How a request flows

1. The browser hits a React Router **loader/action**, which calls the API **only server-side** via
   `app/lib/api.server.ts` (a typed wrapper over the generated client) — never from the browser
   ([ADR-0003](docs/adr/0003-internal-api-via-ssr.md)).
2. A minimal-API endpoint dispatches a **command or query** through `IRequestDispatcher`.
3. Validators run; a handler executes the use case and returns a **`Result<T>`** — expected failures
   (not-found, conflict, validation) are values, not exceptions ([ADR-0013](docs/adr/0013-result-objects-cqrs.md)).
4. Writes go through an aggregate (a new immutable instance per transition) and a repository; the
   **coordinating unit of work** commits every module's write context in one transaction and dispatches
   domain events so cross-module reactions commit atomically ([ADR-0016](docs/adr/0016-aggregate-decomposition-events-per-module-persistence.md)).
5. Reads are **Application-composed projections** over a no-tracking `IReadDataContext` ([ADR-0015](docs/adr/0015-read-data-context.md)).
6. The endpoint maps the `Result` to typed HTTP responses; the C# DTOs are the single source of truth for
   the **build-time OpenAPI document**, from which the C# and TypeScript clients are regenerated on every
   build ([ADR-0010](docs/adr/0010-openapi-contract-generated-clients.md)).

### Opinionated guardrails

- **The domain is immutable, by build error.** A Roslyn analyzer (`Acme.DomainAnalyzers`) is auto-wired
  into every `*.Domain` project and fails the build on mutable state. Six rules, all errors:
  `ACME001` mutable state · `ACME002` records-only · `ACME003` no mutable enumerables ·
  `ACME004` `RaiseEvent<TSelf>` must pass the calling type · `ACME005` no public `init` ·
  `ACME006` a domain type must not be mapped as an EF entity (map an Application `*Entity` instead).
  See [ADR-0017](docs/adr/0017-immutable-domain-enforced-by-analyzer.md).
- **Reproducible builds.** Solution-wide target framework, central package versions, committed NuGet
  lockfiles, and CSharpier/Biome formatting ([ADR-0011](docs/adr/0011-build-hygiene-formatting-lockfiles.md)).
- **No client/contract drift.** Nothing generated is committed; every build re-derives the OpenAPI spec
  and both clients from the C# source.
- **Realtime without exposing the API.** A generic SignalR `NotificationsHub` (`/hubs/notifications`) lets
  a module publish a `NotificationMessage` via `INotificationPublisher`; the browser subscribes through the
  SSR origin's proxy. The Greetings/Widgets pages show a toast when a record is created.

### The example modules

Both are persisted modules that demonstrate the full stack — create / get / list, their own schema and
migration, and a realtime notification on create:

- **Greetings** — `Greeting { Id, Message, CreatedAt }` at `/api/greetings`.
- **Widgets** — `Widget { Id, Name, Quantity, CreatedAt }` at `/api/widgets`.

---

## Project layout

```
Acme.slnx                         # XML solution
Directory.Build.props/.targets    # solution-wide TFM, analyzer auto-wiring
Directory.Packages.props          # central package versions
pnpm-workspace.yaml               # single root pnpm workspace (acme-web, scripts, tests/e2e)
package.json / pnpm-lock.yaml     # root manifest + one lockfile; node_modules hoisted here
biome.json                        # single root Biome config (web + scripts + tests/e2e)
src/
  Aspire/Acme.AppHost             # orchestrator (entry point)
  Aspire/Acme.ServiceDefaults     # OpenTelemetry, health checks, service discovery
  Services/Acme.Api               # minimal-API host; composes modules + OpenAPI + SignalR hub
  Services/acme-web               # React Router SSR frontend (workspace member, i18n en/nl)
  Kernel/Acme.Kernel.*            # shared Domain/Application/Contracts/Infrastructure
  BuildingBlocks/Acme.*           # CQRS(.Abstractions), DomainAbstractions, Http, DomainAnalyzers
  Modules/Acme.Modules.<M>.*      # capability modules (Greetings, Widgets)
  Misc/Acme.ApiClient             # NSwag-generated C# client (build artifact)
scripts/                          # workspace member: web build helper (generate-api-types.ts)
tests/                            # xUnit v3 + MTP v2, incl. NetArchTest boundary rules
tests/e2e/                        # workspace member (package `e2e`): Playwright suite
docs/                             # ADRs + developer instructions
```

---

## Documentation

- [Architecture Decision Records](docs/adr/README.md) — the *why* behind every choice above.
- [Developer instructions](docs/instructions/README.md) — backend & frontend how-tos, local development,
  debugging, testing, and the project-structure / add-a-module guide.

## Make it yours

**Run the AI setup skill — it drives the whole thing.** It asks what you're building, then runs the
rename script for you and does the parts that need judgement: scaffolding real modules for your kind of
app (replacing the `Greetings`/`Widgets` examples), generating their EF migrations, regenerating the
OpenAPI + TypeScript/C# clients, and rewriting this README.

- **Claude Code:** `/setup-template`
- **GitHub Copilot:** the `setup-template` prompt (`.github/prompts/`)
- **Codex / Gemini / Cursor:** see `AGENTS.md` / `GEMINI.md` / `.cursor/rules/`

All five share one source of truth: [`docs/template-setup.md`](docs/template-setup.md).

### The rename script (run directly, if you prefer)

The mechanical rename lives in a cross-platform script (macOS, Linux, Windows; needs only the .NET 10
SDK). The skill runs it for you, but you can run it yourself:

```bash
dotnet run setup.cs            # interactive
# or: ./setup.sh   (macOS/Linux)   ·   .\setup.ps1   (Windows)
dotnet run setup.cs -- --dry-run   # preview every change, write nothing
```

It asks where to create the project (rename **in place**, or `--target=<dir>` to copy the template
to a fresh location first and leave this one untouched), then asks for — and rewrites everywhere — the
values you'd otherwise hand-edit across 200+ files:

| Prompt | Default | Renames |
|---|---|---|
| Namespace / `.slnx` prefix | `Acme` | all `Acme.*` projects, namespaces, folders, `Acme.slnx` |
| Analyzer prefix | `ACME` | the `ACME001…` domain-analyzer diagnostic ids |
| Frontend folder / web resource | `acme-web` | the folder, `package.json`, Aspire resource, CI, Biome |
| API resource | `acme-api` | the Aspire resource + SSR discovery var |
| Database name (kebab; also the Aspire resource name) | `acme` | `AddDatabase` + every `GetConnectionString` |
| Project title | — | README H1 |

On a successful run it also removes the template's `LICENSE` (this one) so the generated project
starts license-free — add your own if you need one. It can wipe/initialize git history
(`--reinit-git`) and remove itself when done (`--remove-tooling` — deletes the script, wrappers,
skill files, and its own CI job). The script's behaviour is covered by `setup.test.cs`
(`dotnet run setup.test.cs`).

## License

Released under the [MIT License](LICENSE) — free to use, modify, and distribute, including in
commercial projects.
