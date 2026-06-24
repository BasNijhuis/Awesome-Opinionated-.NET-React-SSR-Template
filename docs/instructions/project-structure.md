# Project structure

```
.
├── Acme.slnx                      # XML solution (.NET 10+)
├── Directory.Build.props          # solution-wide TFM, central PM, lock files
├── Directory.Build.targets        # auto-wires the domain analyzer per project
├── Directory.Packages.props       # pinned NuGet versions
├── global.json                    # MTP test runner
├── .config/dotnet-tools.json      # local tools (CSharpier)
├── .csharpierignore               # formatter exclusions (EF migrations)
├── package.json                   # root pnpm workspace — packageManager + root lint/format scripts
├── pnpm-workspace.yaml            # workspace members (acme-web, scripts, tests/e2e) + catalog + allowBuilds
├── pnpm-lock.yaml                 # single committed lockfile for the whole workspace
├── biome.json                     # single root Biome config (web + scripts + tests/e2e)
├── .npmrc                         # pnpm config (verify-deps-before-run=false)
├── node_modules/                  # hoisted to repo root (git-ignored)
├── .vscode/                       # launch.json, tasks.json
├── docs/
│   ├── adr/                       # architecture decision records
│   └── instructions/              # this folder
├── scripts/                       # pnpm workspace member `scripts` — web build helper (generate-api-types.ts)
├── src/                          # modular monolith — see ADR-0014. .slnx solution folders in (parens)
│   ├── Services/Acme.Api/          # HTTP host — composes modules + OpenAPI + the NotificationsHub (Services)
│   ├── Services/acme-web/         # React Router SSR (pnpm) (Services; not a .slnx project)
│   ├── Misc/Acme.ApiClient/        # NSwag-generated C# client (obj/, git-ignored) (Misc)
│   ├── Aspire/Acme.{AppHost,ServiceDefaults}/   # (Aspire)
│   ├── Kernel/Acme.Kernel.{Domain,Application,Contracts,Infrastructure}/  # (Kernel) shared primitives (typed ids/enums, DomainEvents), ports, cross-module DTOs, coordinating UoW
│   ├── BuildingBlocks/Acme.{DomainAbstractions,CQRS.Abstractions,CQRS,Http,DomainAnalyzers}/  # (BuildingBlocks) Result/Error, dispatcher, endpoint helpers, immutable-domain analyzer
│   └── Modules/Acme.Modules.<Greetings|Widgets>.<Domain|Domain.Contracts|Application.Contracts|Application|Infrastructure|Endpoints>/  # (Modules; see ADR-0018)
└── tests/
    ├── TestProjects.props              # shared xUnit v3 + adapters config
    ├── Acme.Architecture.Tests/        # NetArchTest boundary rules (ADR-0014)
    ├── Acme.Api.Tests/
    ├── Acme.Modules.<M>.*.Tests/       # per-module domain / persistence tests
    └── e2e/                            # pnpm workspace member `e2e` — Playwright suite (specs + playwright.config.ts)
```

## Modules & boundaries (ADR-0014)

The backend is a **modular monolith**: capability modules (`Greetings`, `Widgets` in this template) over a shared kernel. Each module is a set of layer projects — `.Application.Contracts` (public: commands/queries/result DTOs), `.Domain.Contracts` (public: the domain's input **spec interfaces**, `I…Spec`, present where the module has command-driven specs), `.Domain`/`.Application`/`.Infrastructure` (internal), `.Endpoints` (public minimal-API). The contract layers ([ADR-0018](../adr/0018-domain-spec-interfaces-contract-layering.md)): `.Application.Contracts` references `.Domain.Contracts` (the abstract input ports) but **never the module `.Domain`** (assembly-test enforced); `.Domain` references its own `.Domain.Contracts`. **A module may reference only the shared kernel/building blocks + other modules' contract projects** — never another module's internals. Cross-module reactions go through the dispatcher, an in-process **domain event** (`IDomainEventHandler<T>`), or a kernel port. `tests/Acme.Architecture.Tests` (NetArchTest) fails the build on violations.

Each capability owns its own aggregate ([ADR-0016](../adr/0016-aggregate-decomposition-events-per-module-persistence.md)): `Greeting` (Greetings), `Widget` (Widgets). Each **persistent** module owns a write `<M>DbContext` + a no-tracking `<M>ReadDbContext`, its own PostgreSQL **schema** + migration history — Greetings → `greetings`, Widgets → `widgets`; a coordinating unit of work (`AcmeUnitOfWork`) commits all write contexts in one transaction.

**Adding a module:** create `Acme.Modules.<Name>.{Application.Contracts,Application,Infrastructure,Endpoints}` (+ `.Domain` if it owns domain rules, + `.Domain.Contracts` if its commands drive multi-parameter domain methods — [ADR-0018](../adr/0018-domain-spec-interfaces-contract-layering.md)); add an `AssemblyMarker` to each; expose `Add<Name>Module()` (Infrastructure) and `Map<Name>Endpoints()` (Endpoints); compose both in `Api/Program.cs`; add the projects to the `Modules` slnx folder and the arch-test references.

## Dependency graph (.NET)

```
                    ┌─────────────┐
                    │    Api      │  composes all modules + the NotificationsHub
                    └──────┬──────┘
           ┌───────────────┼───────────────┐
           ▼               ▼               ▼
   ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
   │ Endpoints    │ │ Infrastructure│ │ServiceDefaults│
   └──────┬───────┘ └──────┬───────┘ └──────────────┘
          │                │
          ▼                ▼
   ┌──────────────┐ ┌──────────────┐
   │ Application  │ │  Application │
   └──────┬───────┘ └──────────────┘
          ▼
   ┌──────────────┐
   │   Domain     │
   └──────────────┘
```

**Rule:** `Domain` references only `DomainAbstractions`; the kernel references no module.

## Aspire wiring

```
AppHost
 ├── postgres (Docker + volume)
 ├── acme-api  ← internal only
 └── acme-web ← external, WithReference(api)
```

## Frontend structure

```
acme-web/
├── app/
│   ├── routes/           # file-based routes
│   │   └── home.tsx
│   ├── lib/
│   │   ├── config.server.ts   # API base URL (server only)
│   │   ├── api.server.ts      # thin SSR adapter over the generated client
│   │   ├── i18n.ts            # locale resolution (en / nl)
│   │   └── api/generated/     # generated API client (git-ignored)
│   ├── locales/{en,nl}/   # i18next JSON namespaces (common, home, greetings, widgets, errors)
│   └── root.tsx
├── openapi-ts.config.ts  # @hey-api/openapi-ts codegen config
└── package.json          # workspace member `acme-web` — build/typecheck/lint/test/dev scripts
```

Vitest unit tests stay co-located under `app/**`. Biome config, the lockfile, and `node_modules`
live at the repo root (single workspace); the Playwright suite and the contract-types build helper
have moved to the `tests/e2e` and `scripts` workspace members.

## Naming conventions

| Area | Convention |
|------|------------|
| .NET projects | `Acme.{Layer}` PascalCase |
| Aspire resource names | kebab-case (`acme-api`) |
| Frontend folder | kebab-case (`acme-web`) |
| API routes | `/api/{resource}` lowercase |
| Server-only TS | `*.server.ts` suffix |

## Related

- [ADR index](../adr/README.md)
