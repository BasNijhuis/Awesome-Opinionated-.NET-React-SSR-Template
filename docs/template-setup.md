# Template setup — agent instructions (canonical)

This is the **single source of truth** for the AI side of initializing a new project from the
Awesome Opinionated Template. Every assistant entrypoint — Claude (`.claude/skills/setup-template`),
Copilot (`.github/prompts/setup-template.prompt.md`), Codex (`AGENTS.md`), Cursor
(`.cursor/rules/setup-template.mdc`), Gemini (`GEMINI.md`) — points here. Edit this file only;
the entrypoints stay thin.

The mechanical rename is **not** your job — the `setup.cs` script owns it. You own the parts that
need judgement: scaffolding real modules for the user's kind of app, regenerating derived artifacts,
and rewriting the prose. Do the steps in order; stop and ask if a decision is genuinely the user's.

---

## Step 0 — Run the rename script yourself

The deterministic renames (namespace prefix, analyzer prefix, frontend folder, Aspire resource
names, database name, title) are handled by the script — but **you run it, you don't tell the user to
run it.** Don't perform these renames by hand either; always go through the script.

First, check repo state:

- If a `<Prefix>.slnx` already exists (no `Acme.slnx`), the rename has been done — skip to Step 1.
- If `Acme.slnx` **still exists**, do the following.

1. **Collect the inputs.** Ask the user for the namespace prefix and **where the project should be
   created** (a target directory, or in place); derive sensible defaults for the rest and confirm them
   in one go (analyzer prefix = upper-cased prefix; web = `<kebab>-web`; api = `<kebab>-api`; db =
   `<kebab>`; title = the prefix). Let them override any. Only the prefix is strictly required.
   (The template's `LICENSE` is removed on a successful run — the new project starts license-free.)
   - If they give a **target directory**, pass `--target=<dir>`: the script copies the template there
     (skipping `.git`/build artifacts), renames inside the copy, and leaves this template untouched.
     The rest of your work (Steps 1–7) then happens **in that target directory** — `cd` into it.
   - If they want it **in place**, omit `--target` (it rewrites this checkout directly).
2. **Preview, then apply.** Run the script yourself via your shell/command tool, non-interactively,
   passing every value as a flag. Do a dry run first and check the planned-changes output, then apply:

   ```bash
   dotnet run setup.cs -- --dry-run --yes --prefix=Contoso --analyzer-prefix=CTSO \
     --web=contoso-web --api=contoso-api --db=contoso --title="Contoso Platform"

   dotnet run setup.cs -- --yes --prefix=Contoso --analyzer-prefix=CTSO \
     --web=contoso-web --api=contoso-api --db=contoso --title="Contoso Platform" \
     --target=../contoso
   ```

   Pass `--yes` so the script never blocks on a prompt (you've already gathered the answers). Add
   `--target=...` for a new location. Do
   **not** pass `--reinit-git` or `--remove-tooling` here — those are end-of-flow choices offered in
   Step 7 (note: a `--target` copy already initializes a fresh git repo by default).
3. **Verify it took.** Confirm `Acme.slnx` is gone and a `<Prefix>.slnx` exists (in the target dir if
   you used `--target`), and that the script exited 0. If it failed (e.g. not run from the repo root,
   or a non-empty target), fix the inputs and retry rather than hand-editing files. From here on, work
   in whichever directory now holds `<Prefix>.slnx`.

Throughout the rest of this doc, `<Prefix>` is the chosen namespace prefix and `<web>` the chosen
frontend folder name. Read them off the current `.slnx` filename and `src/Services/` rather than
assuming `Acme`/`acme-web`.

---

## Step 1 — Ask what kind of app this is (skippable)

Ask one open question: **"What are you building?"** — domain, a sentence or two of scope, and the
core nouns (the things users create/manage). Examples: "a booking system for padel courts",
"an internal expense-approval tool", "a B2B invoicing SaaS".

- If they **skip**, leave the example `Greetings` and `Widgets` modules in place as the learning
  recipe, do Steps 5–7 lightly (README identity + build check), and stop. Don't delete the examples
  on a skip.
- Otherwise, translate the domain into **1–3 capability modules** (start small — one solid module
  beats three stubs) and their aggregates. Confirm the module list with the user before generating.

A "module" = one capability that owns an aggregate, its persistence (a PostgreSQL schema), and its
endpoints. Map nouns to aggregates: *booking* → `Bookings` module with a `Booking` aggregate; *court*
→ maybe `Courts` with a `Court` aggregate, etc.

---

## Step 2 — Scaffold each new module (replace the examples)

The authoritative how-to already lives in the repo — **read these first** and mirror their patterns
exactly; they are enforced by analyzers and `tests/<Prefix>.Architecture.Tests` (NetArchTest), so
deviations fail the build:

- `docs/instructions/project-structure.md` — the **"Adding a module"** checklist and the dependency
  graph.
- `docs/adr/0014-modular-monolith.md` — module boundaries (a module may reference only the shared
  kernel/building blocks + **other modules' contract projects**, never their internals).
- `docs/adr/0018-domain-spec-interfaces-contract-layering.md` — `Domain.Contracts` vs
  `Application.Contracts`, the `I…Spec` input ports.
- `docs/adr/0016-aggregate-decomposition-events-per-module-persistence.md` — one aggregate +
  one schema + one migration history per persistent module.
- `docs/adr/0017-immutable-domain-enforced-by-analyzer.md` — the domain must be immutable
  (the `<ANALYZER>0xx` diagnostics enforce this).

Use the existing **`Greetings`** module as the copy-from template — it is the simplest complete
vertical slice. For a new module `<Name>` create, under `src/Modules/`:

- `<Prefix>.Modules.<Name>.Domain.Contracts` — the `I…Spec` input port(s) + the strongly-typed id
  (`<Name>Id`) and its JSON converter. *(Only if commands drive multi-parameter domain methods.)*
- `<Prefix>.Modules.<Name>.Domain` — the aggregate (immutable), its state interface, domain events.
- `<Prefix>.Modules.<Name>.Application.Contracts` — public commands/queries + result DTOs.
- `<Prefix>.Modules.<Name>.Application` — handlers (`Features/<Verb>/…`), validators, the repository
  + read-context interfaces, the `*Entity` (EF-facing, **not** the domain type), error definitions.
- `<Prefix>.Modules.<Name>.Infrastructure` — `<Name>DbContext` (+ `Schema = "<name>"`), read context,
  EF repository, persistence mapper, transactional participant, `DependencyInjection` exposing
  `Add<Name>Module(IConfiguration)`. Add an EF migration (Step 3).
- `<Prefix>.Modules.<Name>.Endpoints` — minimal-API endpoints exposing `Map<Name>Endpoints()`.
- Give every project an `AssemblyMarker.cs` (the dispatcher and arch tests discover assemblies
  through it).

Then **wire it in** (this is the part the project-structure doc calls "two lines"):

1. `src/Services/<Prefix>.Api/Program.cs` — `builder.Services.Add<Name>Module(builder.Configuration);`
   and `app.Map<Name>Endpoints();`.
2. `src/Services/<Prefix>.Api/DependencyInjection.cs` — add the new `<Name>Id` to the
   `StronglyTypedIds` set so OpenAPI emits it as a bare uuid.
3. `<Prefix>.slnx` — add the projects under the `/src/Modules/` folder (and tests under
   `/tests/Modules/`).
4. `tests/<Prefix>.Architecture.Tests` — add the new module's assemblies to the boundary references.
5. Frontend (`src/Services/<web>/app/`): add a route under `routes/` (+ register it in `routes.ts`),
   plus a locale namespace under `locales/en/` and `locales/nl/` (the app ships i18n en/nl). Use the
   existing `greetings.tsx` / `widgets.tsx` routes as the copy-from.

Add a per-module test project under `tests/Modules/` mirroring
`tests/Modules/<Prefix>.Modules.Greetings.Domain.Tests`.

## Step 3 — Generate EF migrations for new persistent modules

Each persistent module owns its own migration history. For each new module:

```
dotnet ef migrations add Initial<Name>Schema \
  --project src/Modules/<Prefix>.Modules.<Name>.Infrastructure \
  --startup-project src/Services/<Prefix>.Api \
  --context <Name>DbContext
```

Mirror the naming of the example migrations. The API migrates every registered `DbContext` on
startup, so no further wiring is needed.

## Step 4 — Remove the example modules cleanly

Once the new modules build, delete `Greetings` and `Widgets` end to end so nothing dangles:

- Project folders under `src/Modules/` and `tests/Modules/` for both modules.
- Their entries in `<Prefix>.slnx` and in the arch-test references.
- In `Program.cs`: the `AddGreetingsModule` / `AddWidgetsModule` and
  `MapGreetingsEndpoints` / `MapWidgetsEndpoints` lines and their `using`s.
- In `DependencyInjection.cs`: `GreetingId` / `WidgetId` from `StronglyTypedIds` (and `using`s).
- Frontend: the `greetings.tsx` / `widgets.tsx` routes (+ `routes.ts` entries), their locale files
  under `locales/{en,nl}/`, and any links in `home.tsx` / `AppShell.tsx`.
- Their API integration tests under `tests/Services/<Prefix>.Api.Tests/` (e.g. `GreetingsFlowTests`,
  `WidgetsFlowTests`).

If the user **skipped** Step 1, do **not** do this — keep the examples.

## Step 5 — Regenerate derived artifacts

These are generated, never hand-edited — regenerate after the backend changes:

- OpenAPI doc + typed clients: `pnpm --filter <web> run api:generate`
  (rebuilds `<Prefix>.Api`, re-exports `openapi/v1.json`, regenerates the TS client under
  `app/lib/api/generated/`, and refreshes the NSwag C# client in `<Prefix>.ApiClient`).
- If you changed routes/locales, run `pnpm --filter <web> run typecheck` and `pnpm --filter <web> run lint`.

## Step 6 — Rewrite the identity prose

The script swapped tokens; it did **not** rewrite meaning. Update:

- `README.md` — the intro paragraph(s) and the "two trivial example modules" line should now
  describe the user's actual app and its real modules. Keep the stack table and ADR links intact.
- `docs/instructions/project-structure.md` — the module list line(s) that named Greetings/Widgets.
- `LICENSE` — the script removes the template's license; add your own if the project needs one.

Leave the ADRs themselves alone — they document *why* the architecture is the way it is and remain
true regardless of domain.

## Step 7 — Verify, then hand back

- Backend: `dotnet build <Prefix>.slnx` then `dotnet test`.
- Frontend: `pnpm install` once at the repo root, then
  `pnpm --filter <web> run build && pnpm --filter <web> run test`.
- Format: `dotnet csharpier .` (or the repo's format task) and `pnpm run format` (root script).
- Optionally run the full app via Aspire to smoke-test (see `docs/instructions/local-development.md`).
- Offer to commit, and — if the user wants a clean slate — to re-init git history
  (`dotnet run setup.cs -- --reinit-git`, or just `rm -rf .git && git init`).
- Offer to remove the setup tooling now that it's done (`setup.cs`, `setup.test.cs`, the wrappers, and
  these skill files) — the script's `--remove-tooling` flag, or do it by hand. The flag also strips the
  **"Setup script (tests)"** job from `.github/workflows/ci.yml`; if you remove the tooling by hand,
  delete that job too so CI doesn't fail on the now-missing `setup.test.cs`.

Report what you changed, what built/passed, and anything you skipped. Don't claim success on a step
whose build or tests you didn't actually run.
