# Frontend development

## Stack

- **React Router Framework 8** — SSR, file-based routes
- **pnpm 11.8** — package manager
- **Tailwind CSS 4** — styling (from template)

## Commands

```powershell
cd src/Services/acme-web

pnpm install          # after clone or package.json changes
pnpm run dev          # dev server (Aspire uses this via WithRunScript("dev"))
pnpm run build        # production build
pnpm run start        # serve production build (custom server.js — proxies /hubs to the API)
pnpm run test:e2e     # Playwright E2E (boots the full stack via Aspire)
pnpm run typecheck    # regenerate API client + types + route gen
pnpm run lint         # Biome check (lint + format diagnostics)
pnpm run format       # Biome check --write (apply fixes)
pnpm run api:generate # build API + regenerate the typed client (run by dev/build/typecheck)
```

## Linting & formatting

**Biome** is the frontend linter + formatter ([ADR-0012](../adr/0012-biome-frontend-lint-format.md)) — config in `biome.json`. Run `pnpm run format` before pushing; CI can run `pnpm run lint`. Biome respects `.gitignore` and additionally skips the generated API client and Tailwind CSS (its parser doesn't handle Tailwind 4 at-rules — Tailwind owns the CSS). C# is formatted separately by CSharpier ([ADR-0011](../adr/0011-build-hygiene-formatting-lockfiles.md)).

## Routes

Routes live in `app/routes/`. Register in `app/routes.ts`:

```typescript
import { type RouteConfig, index, route } from "@react-router/dev/routes";

export default [
  index("routes/home.tsx"),
  route("greetings", "routes/greetings.tsx"),
  route("widgets", "routes/widgets.tsx"),
] satisfies RouteConfig;
```

## Server vs client boundary

**Critical rule (ADR-0003):** the browser must never call the API directly.

| File pattern | Runs on | Can call API? |
|--------------|---------|---------------|
| `*.server.ts` | Node SSR server | Yes |
| `*.tsx` loaders/actions | Server | Yes (via `*.server.ts`) |
| Client components | Browser | **No** |

### API client (generated)

The TypeScript client is **generated from the API's OpenAPI document** with `@hey-api/openapi-ts` — never hand-write DTOs (see [ADR-0010](../adr/0010-openapi-contract-generated-clients.md)).

- `openapi-ts.config.ts` reads the spec at `../Acme.Api/openapi/v1.json` (a `dotnet build` artifact) and emits the client into `app/lib/api/generated/`.
- **Both the spec and `app/lib/api/generated/` are git-ignored build artifacts.** `pnpm run api:generate` (run automatically by `dev`/`build`/`typecheck`/`prepare`) does `dotnet build` → `openapi-ts`, so a fresh clone is self-contained with no running API. Never hand-edit the generated files.
- After changing a C# DTO/endpoint, just re-run any of those scripts — types and SDK functions regenerate. There is no hand-authored type left.
- `app/lib/api.server.ts` is a thin **SSR configurator**: it wraps the generated SDK functions, injects the base URL from `config.server.ts` per call, maps errors to `ApiError`, and re-exports the generated DTO types under their existing names.

```typescript
// app/lib/api.server.ts — thin wrappers over the generated SDK
export async function getGreeting(id: string) {
  return unwrap(await api.getGreeting({ baseUrl: getApiBaseUrl(), path: { id } }));
}
```

Use in route loaders/actions exactly as before (`getGreeting`, `createGreeting`, `listWidgets`, … unchanged signatures).

### Standalone dev

Create `src/Services/acme-web/.env` (gitignored):

```
API_URL=http://localhost:5xxx
```

## Realtime notifications

Live updates use SignalR on the **web origin** (`/hubs/notifications`), proxied to the internal API in dev so the browser never reaches the API directly ([ADR-0003](../adr/0003-internal-api-via-ssr.md)). The backend publishes a `NotificationMessage(channel, message)` (a module raises it via the kernel `INotificationPublisher`); the client subscribes to the channels it cares about and renders incoming messages (e.g. a toast). REST mutations still go through SSR actions only.

## Localization (en / nl)

The app ships **English** (default) and **Dutch**, via **i18next + react-i18next**.

- **Locale resolution** — `resolveLocale(request)` in `app/lib/i18n.ts`: cookie `locale` → `Accept-Language` → `en`. The root loader resolves it and renders `<html lang>`; `entry.server.tsx` / `entry.client.tsx` build a per-request/once i18next instance from the same locale, so first paint and hydration match (resources are bundled, no flash, no http backend).
- **UI strings** — JSON namespaces under `app/locales/{en,nl}/` (`common`, `home`, `greetings`, `widgets`, `errors`). Use `useTranslation(ns)` + `t("ns:key")`; never hard-code user-facing English in JSX. Pluralize with i18next `_one`/`_other` keys.
- **Switching** — the `LanguageToggle` posts to the `set-locale` resource route, which sets the `locale` cookie and redirects back (works without JS). The choice survives a hard reload.
- **API errors** — the API returns a stable `errorCode` on every `ProblemDetails`. The SSR layer maps `errorCode` → `errors.json` via `getErrorMessage(error, translate)` / `getErrorTranslator(request)`; unmapped codes fall back to the English `detail`. Never parse the English message text.

## Styling

**Tailwind CSS 4** is the only styling system — no CSS modules, no second styling library, and **no `tailwind.config.js`** (Tailwind 4 is CSS-first in `app/app.css`).

**Every color is a token.** The palette is defined once in the `@theme` block of `app/app.css`; **no literal hex** lives outside `@theme` (in CSS rules, JSX, or SVG). Reference tokens as Tailwind utilities (`bg-surface`, `text-accent`) or via `var(--color-…)` in inline styles / SVG attributes.

Shared component classes live in `app/app.css` — extract a `@layer`/component class when a pattern repeats **3+ times**; use inline Tailwind utilities for one-off layout. Keep `@apply` inside `app.css` only. Interactive controls carry `focus-visible` rings, and decorative animations are disabled under `prefers-reduced-motion`.

**Fonts are self-hosted** — bundled woff2 via `@fontsource*` packages, `@import`ed at the top of `app/app.css`. **No Google Fonts CDN** — there are no external font `<link>`s in `app/root.tsx`. Add weights by importing the matching `@fontsource/*` CSS, not by adding a remote stylesheet.

## Aspire integration

AppHost registers the app:

```csharp
builder.AddJavaScriptApp("acme-web", "../acme-web")
    .WithPnpm()
    .WithRunScript("dev")
    .WithReference(api)
    .WithExternalHttpEndpoints();
```

The run script and port are dynamic by default: when `E2E_WEB_PORT` is set the AppHost pins the port and runs the built production server (`start`) instead of `dev` — see the Playwright section below.

## Do not commit

- `node_modules/`
- `.env` / `.env.local`
- `build/` (production output)
- `app/lib/api/generated/` (generated API client — a build artifact)

**Do commit:** `pnpm-lock.yaml`

## Testing frontend changes

Unit tests run on **Vitest + React Testing Library**:

- `pnpm run test` (CI/one-shot) and `pnpm run test:watch` (local) — runs `app/**/*.test.{ts,tsx}`.
- Config is a **standalone `vitest.config.ts`** that deliberately omits the `reactRouter()` Vite plugin (it transforms routes and conflicts with the runner); environment is `jsdom`, with `@testing-library/jest-dom` matchers wired in `vitest.setup.ts`.
- **What to test:** pure helpers (e.g. `getErrorMessage`) and prop-driven components. Keep `*.server.ts` server-only logic out of unit tests — extract pure functions into a plain module or mock `config.server`/`fetch`. **Never hit a real API.** SSR loaders/actions against a live API and full browser flows are E2E (out of scope here).

When a task changes **behavior** (client logic, error handling, a component's rendering), add or update a unit test. At minimum before marking a frontend task done:

- `pnpm run test` passes
- `pnpm run typecheck` passes
- `pnpm run lint` passes (Biome)
- Manual smoke test of the affected route(s)

Backend behavior introduced for the UI should still be covered by domain/API tests. See [Testing — when finishing a task](./testing.md#when-finishing-a-task).

## End-to-end tests (Playwright)

Full browser flows run on **Playwright** — specs live in `e2e/*.spec.ts` with shared flow helpers in `e2e/support/`. They drive the app the way a user does and can assert realtime updates arrive over SignalR.

```bash
pnpm run test:e2e        # headless, boots the whole stack
pnpm run test:e2e:ui     # Playwright inspector
```

How it runs:

- **Aspire orchestrates everything.** Playwright's `webServer` runs `dotnet run` on the AppHost, which brings up Postgres + the API + the web. Targets only the web origin ([ADR-0003](../adr/0003-internal-api-via-ssr.md)) — never the internal API directly.
- **Production server, not the dev server.** Setting `E2E_WEB_PORT` (the AppHost reads it) pins the web to a fixed port **and** switches it to a built production run (`build` then `node server.js`). That matters because the dev-only `/hubs` proxy in `vite.config.ts` does not exist in a build — `server.js` carries its own `/hubs` proxy (with websocket upgrade) so SignalR works at the web origin in production. Without `E2E_WEB_PORT` the AppHost is unchanged (hot-reloading `dev` on a dynamic port).
- **Docker required**, and the first run is slow (Postgres + a full `react-router build`) — the `webServer` timeout is generous. If you already have a local `aspire run` up, stop it first: the pinned port won't match it.
- **State isolation:** Postgres uses a data volume (not pristine between runs), so each test creates its **own** data rather than relying on a clean database.
- **Selectors** are stable `data-testid`s on the routes/components; prefer `getByTestId` in specs over brittle text.

## Related ADRs

- [ADR-0003: Internal API via SSR](../adr/0003-internal-api-via-ssr.md)
- [ADR-0004: React Router + pnpm](../adr/0004-react-router-ssr-pnpm.md)
- [ADR-0010: OpenAPI contract & generated clients](../adr/0010-openapi-contract-generated-clients.md)
- [ADR-0012: Frontend lint & format with Biome](../adr/0012-biome-frontend-lint-format.md)
