# Testing

## When finishing a task

**A task is not done until tests are written and passing, when applicable.**

Treat tests as part of the deliverable—not a follow-up. Before marking a task complete or opening a PR:

1. **Decide if tests apply** (see below).
2. **Add or update tests** for the behavior you changed.
3. **Run the suite** from repo root:

```powershell
dotnet test --solution Acme.slnx
```

For frontend-only work, also run `pnpm run test` (Vitest + React Testing Library) and `pnpm run typecheck`.

### When tests apply

| Change | Expected tests |
|--------|----------------|
| Domain rules / aggregate behavior | Unit tests in the module domain test project |
| New or changed API endpoints / flows | Integration tests in `Acme.Api.Tests` |
| Frontend pure helpers or prop-driven components | Unit tests in `acme-web` (Vitest + RTL) |
| Bug fix | Regression test that would have failed before the fix |
| Persistence, mapping, or cross-layer behavior | Integration or repository tests |
| Full flow through the browser, or SignalR realtime across clients | E2E specs in `acme-web/e2e` (Playwright) |
| Refactor that preserves behavior | Existing tests must still pass; add tests if coverage was missing |

Use **Arrange–Act–Assert** with a **single action in Act**, one behavior per test, **AwesomeAssertions** (`.Should()`), and deterministic fakes for any injected non-deterministic service (e.g. an `IRandomProvider`) when randomness affects assertions. Build domain test state with a builder (e.g. `GreetingBuilder`) in Arrange rather than calling aggregate methods inline.

### When tests usually do not apply

- Documentation-only changes
- Dependency or tooling bumps with no behavior change
- Pure styling or copy with no logic (verify via build/typecheck instead)
- Exploratory spikes that are explicitly not merged as finished work

If you skip tests, note **why** in the PR or task checklist (e.g. "docs only").

See also: [Backend development — testing](./backend-development.md#testing-backend-changes) · [Frontend development — testing](./frontend-development.md#testing-frontend-changes)

## Run all tests

From repo root:

```powershell
dotnet test --solution Acme.slnx
```

Requires `global.json` with MTP runner (already configured):

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

## VS Code / Cursor Test Explorer

Tests run fine from the CLI. If the Testing panel shows **"Test controller is not available"** or no tests:

### 1. Extensions and solution load

1. Install **C# Dev Kit** (`ms-dotnettools.csdevkit`) — it pulls in the **C#** extension.
2. Reload the window (`Ctrl+Shift+P` → **Developer: Reload Window**).
3. Wait for the solution to load (status bar shows the solution name). Workspace sets `dotnet.defaultSolution` to `Acme.slnx`.

### 2. Build first

Test discovery needs a successful build:

```powershell
dotnet build --solution Acme.slnx
```

Or run the default build task (`Ctrl+Shift+B`).

### 3. MTP + VSTest adapters

CLI `dotnet test` uses **MTP v2** (`global.json`). Test projects import `tests/TestProjects.props`, which adds **VSTest adapters** for VS Code / Visual Studio Test Explorer.

After changing packages or renaming test files, rebuild and reload the window:

```powershell
dotnet build --solution Acme.slnx
```

Check **Output** → **C# Dev Kit - Test Explorer** if tests still do not appear.

## Run a single test project

```powershell
dotnet test --project tests/Acme.Modules.Greetings.Domain.Tests
dotnet test --project tests/Acme.Api.Tests
```

Or run as executable:

```powershell
dotnet run --project tests/Acme.Modules.Greetings.Domain.Tests
```

## Stack

| Item | Package / setting |
|------|-------------------|
| Framework | xUnit v3 (`xunit.v3.mtp-v2` 3.2.2) |
| Analyzers | `xunit.analyzers` (transitive via `xunit.v3.mtp-v2` — no explicit reference needed) |
| Runner | Microsoft Testing Platform v2 (CLI via `global.json`) |
| IDE discovery | `Microsoft.NET.Test.Sdk` + `xunit.runner.visualstudio` (via `tests/TestProjects.props`) |
| API integration | `Microsoft.AspNetCore.Mvc.Testing` 10.0.9 + generated `AcmeApiClient` |

Package versions are managed centrally in `Directory.Packages.props` at the repo root.

## Central package management

NuGet versions are pinned in `Directory.Packages.props`. Projects use `<PackageReference Include="..." />` without a `Version` attribute.

To add a package, edit `Directory.Packages.props` and reference it from the target `.csproj`. Bump versions in one place when upgrading dependencies.

## Project layout

```
tests/
├── Acme.Modules.<M>.Domain.Tests/        # unit tests — domain rules, no I/O
├── Acme.Modules.<M>.Persistence.Tests/   # EF pipeline (Testcontainers)
├── Acme.Architecture.Tests/              # NetArchTest boundary rules
└── Acme.Api.Tests/                       # integration — WebApplicationFactory
```

## Test structure (Arrange–Act–Assert)

Structure every test in three phases, separated by blank lines and `// Arrange`, `// Act`, and `// Assert` comments:

| Phase | Purpose |
|-------|---------|
| **Arrange** | Set up data, dependencies, and preconditions |
| **Act** | Invoke **exactly one** action under test |
| **Assert** | Verify the outcome with `.Should()` |

**Act is a single action.** Everything needed to reach the pre-state — including multi-step setup — goes in Arrange; the one call being tested is Act. Do not chain several actions in Act. Split multi-scenario tests into separate methods (one behavior each).

**Build state with a builder, not inline aggregate calls.** Domain tests use a builder (e.g. `GreetingBuilder`) to declare the starting state; reach for `Result`-returning methods only when they *are* the action under test. Access a successful value with `.Value`.

Since expected failures return a `Result` ([ADR-0013](../adr/0013-result-objects-cqrs.md)), assert on the result — there is no `Assert.Throws` for domain-rule failures.

```csharp
[Fact]
public void AdjustQuantity_within_bounds_succeeds()
{
    // Arrange
    var widget = WidgetBuilder.WithQuantity(5);

    // Act
    var result = widget.AdjustQuantity(3);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value.Quantity.Should().Be(8);
}

[Fact]
public void AdjustQuantity_below_zero_fails()
{
    // Arrange
    var widget = WidgetBuilder.WithQuantity(2);

    // Act
    var result = widget.AdjustQuantity(-5);

    // Assert
    result.IsFailure.Should().BeTrue();
    result.Error.Category.Should().Be(ErrorCategory.Conflict);
}
```

Integration tests follow the same rule: set the data up via the typed client in Arrange, then make the single call under test in Act.

## Assertions (AwesomeAssertions)

Use **AwesomeAssertions** (`.Should()`) for all assertions — `value.Should().Be(...)`, `result.IsFailure.Should().BeTrue()`, `collection.Should().ContainSingle()`. It is wired in globally via `tests/TestProjects.props` (a `<Using Include="AwesomeAssertions" />`), so no per-file `using` is needed. Do not add new raw `Assert.*` calls.

## Writing domain tests

Place tests next to the behavior they protect. Build the starting state with a builder, and use deterministic fakes for any injected non-deterministic service (e.g. `IRandomProvider`) when random outcomes affect assertions.

```csharp
[Fact]
public void Create_rejects_blank_message()
{
    // Arrange
    var message = "   ";

    // Act
    var result = Greeting.Create(message);

    // Assert
    result.IsFailure.Should().BeTrue();
    result.Error.Category.Should().Be(ErrorCategory.Validation);
}
```

## Writing API tests

Use `WebApplicationFactory<Program>`. The API exposes `public partial class Program` for test host bootstrapping.

Pass `TestContext.Current.CancellationToken` to async calls that accept a `CancellationToken` (xUnit v3 analyzer rule **xUnit1051**). This lets the runner cancel hung tests promptly.

```csharp
[Fact]
public async Task Ping_returns_ok()
{
    // Arrange
    var cancellationToken = TestContext.Current.CancellationToken;
    await using var factory = new WebApplicationFactory<Program>();
    using var client = factory.CreateClient();

    // Act
    var response = await client.GetAsync("/api/ping", cancellationToken);
    var payload = await response.Content.ReadFromJsonAsync<PingResponse>(cancellationToken);

    // Assert
    response.EnsureSuccessStatusCode();
    payload.Should().NotBeNull();
    payload!.Status.Should().Be("ok");
}
```

Same applies to `IRequestDispatcher.SendAsync`/`QueryAsync`, `PostAsJsonAsync`, and other async APIs in integration tests.

### Typed client for endpoint flows

Prefer the generated **`AcmeApiClient`** (from `Acme.ApiClient`, see [ADR-0010](../adr/0010-openapi-contract-generated-clients.md)) over hand-written paths and DTOs when testing the API contract. Build it over the factory's `HttpClient`, and use `TestWebApplicationFactory.Create(...)`. Pass a deterministic fake (e.g. a fixed `IRandomProvider`) when a random outcome would otherwise make the flow flaky:

```csharp
await using var factory = TestWebApplicationFactory.Create();
var client = new AcmeApiClient(factory.CreateClient());

var created = await client.CreateGreetingAsync(
    new CreateGreetingRequest { Message = "Hello, world" },
    TestContext.Current.CancellationToken);
```

Raw `HttpClient` is still fine for non-contract checks (e.g. `/api/ping`, the OpenAPI document itself).

## xUnit analyzer warnings

xUnit v3 ships analyzers with the `xunit.v3.mtp-v2` package (it depends on `xunit.analyzers`), so they run without an explicit reference. The most common warning in this repo:

| Rule | Meaning | Fix |
|------|---------|-----|
| **xUnit1051** | Async call accepts `CancellationToken` but none was passed | Pass `TestContext.Current.CancellationToken` |

Example for application-layer tests:

```csharp
var cancellationToken = TestContext.Current.CancellationToken;
var result = await dispatcher.QueryAsync(new MyQuery(), cancellationToken);
result.IsSuccess.Should().BeTrue();
```

Domain unit tests are synchronous and do not trigger this rule.

Reference: [xUnit1051](https://xunit.net/xunit.analyzers/rules/xUnit1051)

## Filtering (MTP)

```powershell
dotnet test --project tests/Acme.Modules.Greetings.Domain.Tests -- --filter-class GreetingTests
```

Note the `--` separator may not be required on .NET 10 SDK with native MTP.

## Testcontainers

Postgres integration tests use **Testcontainers** in the per-module persistence test projects (`Acme.Modules.<M>.Persistence.Tests`), which exercise the real EF pipeline against a throwaway Postgres container. API integration tests use in-memory persistence via `TestWebApplicationFactory` and `Persistence:UseInMemory=true` for speed.

```csharp
// TestWebApplicationFactory — no Postgres required for API tests
builder.UseSetting("Persistence:UseInMemory", "true");
```

## Frontend unit tests (Vitest)

Frontend unit/component tests run on **Vitest + React Testing Library** in `src/Services/acme-web`, separate from the xUnit backend suite.

```bash
cd src/Services/acme-web && pnpm run test        # one-shot (CI)
cd src/Services/acme-web && pnpm run test:watch  # local watch
```

- Runs `app/**/*.test.{ts,tsx}` via a standalone `vitest.config.ts` (jsdom, `@testing-library/jest-dom`); it deliberately omits the `reactRouter()` Vite plugin.
- **Scope:** pure helpers (e.g. `getErrorMessage`) and prop-driven components. Keep `*.server.ts` logic out of unit tests and **never hit a real API** — SSR loaders/actions and full browser flows belong in the Playwright E2E suite below.
- For frontend-only work, run `pnpm run test`, `pnpm run typecheck`, and `pnpm run lint` before finishing. Full how-to: [Frontend development — testing](./frontend-development.md#testing-frontend-changes).

## End-to-end tests (Playwright)

Full browser flows live in `src/Services/acme-web/e2e` and run on **Playwright** — separate from the xUnit suite and from the Vitest unit tests above. They are the right (and only) place to assert **SignalR realtime** behaviour end to end.

```bash
cd src/Services/acme-web && pnpm run test:e2e
```

- **The whole stack runs via Aspire.** Playwright's `webServer` runs `dotnet run` on the AppHost, which brings up Postgres + the API + the web's **production** server (`server.js`, which proxies `/hubs` to the API — the dev-only `vite.config.ts` proxy does not exist in a build). Setting `E2E_WEB_PORT` switches the AppHost into this mode (pinned port, production server). Specs target only the web origin ([ADR-0003](../adr/0003-internal-api-via-ssr.md)).
- **Docker required**; the boot is heavier than a unit run.
- Stable `data-testid`s drive the specs; each test creates its own data for isolation.
- Full how-to (selectors, startup model, flake-avoidance) is in [Frontend development — End-to-end tests](./frontend-development.md#end-to-end-tests-playwright).

## Continuous integration (GitHub Actions)

CI is **build + test only — no deployment.** [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml) runs **on PRs**; three independent jobs run **in parallel** (no cross-job `needs`), each building first then testing — so backend tests don't wait for the frontend build, etc.:

| Job | Runs (mirror locally) |
|-----|------------------------|
| **backend** | `dotnet build Acme.slnx` · `dotnet csharpier check .` · `dotnet test` (Testcontainers — needs Docker). The immutable-domain analyzer fails the build on an `ACME*` violation. |
| **frontend** | `pnpm install --frozen-lockfile` · `pnpm run build` (also generates the OpenAPI client, so the job needs the .NET SDK) · `pnpm run typecheck` · `pnpm run lint` · `pnpm run test:coverage` |
| **e2e** | `pnpm run test:e2e` — boots the full Aspire stack (Postgres + API + the production web server) and runs Playwright. Needs Docker; creates an HTTPS dev cert (`dotnet dev-certs https`, trusted via `SSL_CERT_DIR` on Linux) before Aspire's preflight. |

- **Code coverage is collected and published as artifacts (`backend-coverage`, `frontend-coverage`) but never gates** — no thresholds. Backend uses the MTP `Microsoft.Testing.Extensions.CodeCoverage` extension (`dotnet test … --coverage --coverage-output-format cobertura`); frontend uses `@vitest/coverage-v8` (`pnpm run test:coverage`).
- **Architecture tests run without coverage.** Coverage instrumentation injects an assembly dependency that NetArchTest's "depends only on" / "is a leaf" rules reject, so `Acme.Architecture.Tests` runs on its own (no `--coverage`); every other test project runs with coverage.
- **Backend restore is not `--locked-mode`:** the Aspire AppHost pulls RID-specific packages, so its lock file is platform-specific and would fail locked-mode on a differing CI RID. (The frontend's `pnpm install --frozen-lockfile` is the platform-safe lockfile gate.)
- **The E2E AppHost injects `API_URL` explicitly** (`api.GetEndpoint("http")`): Aspire's `WithReference` discovery var isn't reliably surfaced to the production `node server.js`, and both the SSR client and the `/hubs` proxy read `API_URL` as the fallback.
- **Required checks:** `backend`, `frontend`, and `e2e` should be marked required on `master` (branch protection — a repo-admin setting).
- Everything CI runs is reproducible locally with the commands above.

## Related ADR

- [ADR-0006: xUnit v3 + MTP v2](../adr/0006-xunit-v3-mtp-v2.md)
- [ADR-0013: Result objects + CQRS split](../adr/0013-result-objects-cqrs.md) — AAA single-action, builder, AwesomeAssertions
- [ADR-0010: OpenAPI contract & generated clients](../adr/0010-openapi-contract-generated-clients.md)
- [ADR-0011: Build hygiene (CSharpier, lock files, solution-wide TFM)](../adr/0011-build-hygiene-formatting-lockfiles.md)
