# ADR-0010: API contract via build-time OpenAPI and generated clients

- **Status:** Accepted
- **Date:** 2026-06-20

## Context

The SSR web app and the API tests both consumed the HTTP API through hand-written types and fetch/`HttpClient` calls. Every contract change meant editing C# DTOs **and** hand-maintained TypeScript types (and test helpers), which drift silently. Minimal-API endpoints also returned untyped `IResult`, so an OpenAPI document generated from them would carry no response schemas.

We want a single source of truth for the HTTP contract and clients generated from it.

## Decision

**The C# DTOs and endpoints are the single source of truth. The OpenAPI document and all clients are generated build artifacts — never committed, never hand-edited.**

1. **Typed endpoints.** Endpoints return `TypedResults` (`Ok<T>`, `Results<Created<CreateGreetingResult>, ValidationProblem>`, …), not bare `IResult`. The typed return supplies success/inline-validation responses to OpenAPI automatically. Shared error responses (404/409/422) come from the global `DomainExceptionHandler` middleware and are declared explicitly via a `.ProducesDomainProblems()` helper. Each endpoint sets `.WithName(nameof(Handler))` to give the generated TS client clean function names (the C# client names follow the URL paths).

2. **OpenAPI document.** `AddOpenApi("v1")` + `app.MapOpenApi()` serve `/openapi/v1.json`. `Microsoft.Extensions.ApiDescription.Server` also writes `src/Services/Acme.Api/openapi/v1.json` on every `dotnet build` (`--file-name v1`). The file is **git-ignored**. Build-time generation runs in-memory (migration is guarded on a real connection string) so no database is required.

3. **Generated clients.**
   - **C# (backend/tests):** `src/Misc/Acme.ApiClient` generates `AcmeApiClient` with **Microsoft Kiota** from the spec: an MSBuild target runs the pinned `kiota` local tool (`.config/dotnet-tools.json`) before compile, incrementally on spec changes. `AcmeApiClientFactory.Create(HttpClient)` wires the Kiota request adapter over any `HttpClient`, so it drops onto `WebApplicationFactory.CreateClient()` in API tests. Generated code lives in `obj/Kiota/` (git-ignored); a build-order-only project reference (`ReferenceOutputAssembly="false"`) ensures the spec exists first.
   - **TypeScript (frontend):** generated with **@hey-api/openapi-ts** (fetch client) into `app/lib/api/generated/` (git-ignored). `app/lib/api.server.ts` is a thin SSR adapter over the generated SDK.

4. **Freshness.** Because nothing is committed, there is no drift to guard — every build regenerates the spec and the C# client from source.

## Consequences

### Positive

- One contract source (C#); clients can't silently drift from it.
- API tests exercise the real serialized contract through a typed client.
- The OpenAPI document is always consistent with the running API.

### Negative

- `dotnet build` runs the API once to emit the spec, and the client project regenerates on build (small cost).
- Adds the Kiota local tool + `Microsoft.Kiota.Bundle` and `Microsoft.Extensions.ApiDescription.Server` build dependencies; the client build runs `dotnet tool restore` itself before generating.
- Generated C# request builders follow the URL paths; generated TS function names depend on operation names. Keep routes, `.WithName(...)` and typed results stable.

### Notes

- Supersedes the bare-`IResult` endpoint style shown in [ADR-0008](./0008-custom-request-dispatch.md); the dispatch/validation pipeline itself is unchanged.
- Preserves [ADR-0003](./0003-internal-api-via-ssr.md): the browser still reaches the API only via the SSR server; generation is a build-time/server concern.

### Update — contract refinements for clean codegen

Realizing the TypeScript client surfaced cases where the published schema didn't generate clean types. The contract was adjusted (the C# source of truth still wins):

- **Enums as strings.** A global `JsonStringEnumConverter` plus enum-typed DTO properties emit named string-enum schemas, so generated clients get unions (e.g. `Status = "Active" | …`) instead of `string` — no hand-authored types.
- **Integer schema transformer.** .NET emits `int` as `["integer","string"]` (with a digit pattern) under OpenAPI 3.1; an `AddSchemaTransformer` collapses it to plain `integer` so clients get `number`, not `number | string`. (This is what gives `WidgetDto.Quantity` a clean `number` type.)
- **Always-serialized nullable fields are `required`.** Nullable response fields that are always written are marked `required` so clients type them `T | null`, not `T | null | undefined`.

### Update (2026-10-28) — C# client: NSwag → Kiota

The C# client moved from NSwag to **Microsoft Kiota**: Microsoft's own generator, with a fluent, path-shaped API (`client.Api.Greetings[id].GetAsync()`) and System.Text.Json serialization (drops Newtonsoft.Json). Kiota has no MSBuild integration, so `Acme.ApiClient.csproj` runs the pinned `kiota` local tool from a target; bump the tool and `Microsoft.Kiota.Bundle` together (`dotnet kiota info -l CSharp`).

Trade-offs:

- **Every model property is nullable**, regardless of `required` — a deliberate Kiota design choice with no option to change it. Consumers use `!` / `.Value` (tests) on fields they know are present. The `required` refinements above still shape the TS client.
- **Naming comes from paths**, not `operationId`; `.WithName(...)` now matters only for the TS client.
- **Errors** are generated per response schema, as sibling `ApiException` subclasses: 404/409/422 throw the generated `ProblemDetails`, a 400 throws `HttpValidationProblemDetails`, and an undeclared status throws plain `ApiException`. Catch `ApiException` and check `ResponseStatusCode` unless the body matters. The exception `Message` stays generic (Kiota only fills it from a field marked `x-ms-primary-error-message`); inspect `Detail` / `Title` instead.

## Related

- [ADR-0003](./0003-internal-api-via-ssr.md) · [ADR-0008](./0008-custom-request-dispatch.md)
- [instructions/backend-development.md](../instructions/backend-development.md) · [instructions/frontend-development.md](../instructions/frontend-development.md)
