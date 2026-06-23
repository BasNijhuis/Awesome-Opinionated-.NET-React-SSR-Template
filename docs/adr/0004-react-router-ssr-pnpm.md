# ADR-0004: React Router Framework SSR with pnpm

- **Status:** Accepted
- **Date:** 2026-06-19

## Context

We need a modern React UI with server-side rendering for fast first paint, SEO-friendly landing content, and server-side access to the internal API. We also want deterministic, fast installs and lockfile-based CI.

## Decision

Use **React Router Framework v8** (SSR enabled) with **pnpm 11.x** as the sole frontend package manager.

| Choice | Value |
|--------|-------|
| Framework | React Router 8 (`create-react-router`) |
| Rendering | SSR via `@react-router/node` + Vite |
| Package manager | pnpm (`packageManager: "pnpm@11.8.0"`) |
| Aspire integration | `AddJavaScriptApp(...).WithPnpm().WithRunScript("dev")` |
| Lockfile | `pnpm-lock.yaml` committed to git |

Scripts:

```json
{
  "dev": "react-router dev",
  "build": "react-router build",
  "start": "react-router-serve ./build/server/index.js"
}
```

**Not** using the Aspire template's Blazor frontend—replaced entirely by `src/acme-web`.

## Consequences

### Positive

- SSR loaders prove the internal API path early (`/api/ping` on landing page).
- pnpm is faster and stricter than npm; `--frozen-lockfile` works in CI.
- Aspire 13 `AddJavaScriptApp` + `WithPnpm()` is the supported hosting model (replaces deprecated `AddNpmApp` / `Aspire.Hosting.NodeJs`).

### Negative

- Developers must enable pnpm via Corepack (`corepack enable`).
- `node_modules` is not hoisted by default—occasional compatibility issues with poorly packaged deps.
- React Router v8 SSR has a learning curve (loaders, actions, `*.server.ts` boundary).

### References

- `src/acme-web/`
- [Instructions: Frontend development](../instructions/frontend-development.md)
