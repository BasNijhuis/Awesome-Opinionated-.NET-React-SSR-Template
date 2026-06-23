# ADR-0003: Internal-only API; browser talks to SSR server only

- **Status:** Accepted
- **Date:** 2026-06-19

## Context

The React Router app needs data from the API. Exposing the API URL to the browser would publish an attack surface, require CORS, and leak infrastructure details. We want the browser's same-origin policy to apply: users only interact with the web app.

## Decision

**Only `acme-web` has an external HTTP endpoint.** The API is registered in AppHost **without** `WithExternalHttpEndpoints()`.

```
Browser ──public──▶ acme-web (React Router SSR)
                         │
                         │ internal (Aspire service discovery)
                         ▼
                    acme-api
                         │
                         ▼
                       postgres
```

Implementation rules:

1. **AppHost:** `WithReference(api)` on the JavaScript app injects `services__acme-api__http__0`.
2. **Frontend:** All API calls from `app/lib/*.server.ts` only—never from client components.
3. **Loaders/actions:** Fetch data on the server during SSR or run form mutations there.
4. **Standalone dev:** `API_URL` in `src/acme-web/.env` when not running via AppHost.

```typescript
// app/lib/config.server.ts
process.env["services__acme-api__http__0"] ?? process.env.API_URL
```

## Consequences

### Positive

- No API host/port in the browser bundle.
- CORS is unnecessary for normal use.
- SSR can prefetch data before hydration.

### Negative

- Every new API consumer must go through a server module (`*.server.ts`).
- Client-side live updates use SignalR on the **web origin** (`/hubs/notifications`), proxied to the internal API in dev; REST mutations still go through SSR actions only.
- Debugging API issues requires checking both web server logs and API logs in Aspire dashboard.

### Verification

Open DevTools → Network while the app runs: no REST requests to the API origin; the WebSocket connects to the web origin at `/hubs/notifications`.

### References

- `src/acme-web/app/lib/config.server.ts`
- `src/acme-web/app/lib/api.server.ts`
- [Instructions: Frontend development](../instructions/frontend-development.md)
