# ADR-0006: xUnit v3 + Microsoft Testing Platform v2

- **Status:** Accepted
- **Date:** 2026-06-19

## Context

.NET 10 introduces native **Microsoft Testing Platform (MTP)** as an alternative to VSTest. xUnit v3 supports MTP natively with better performance and executable-first test projects. We want tests aligned with the current .NET 10 toolchain.

## Decision

Use **xUnit.net v3** with the **MTP v2** package variant:

| Item | Value |
|------|-------|
| Template | `dotnet new xunit3 --test-runner mtp-v2` |
| Package | `xunit.v3.mtp-v2` **4.0.1** |
| CLI runner | Microsoft Testing Platform v2 (via `global.json`) |
| IDE adapters | `Microsoft.NET.Test.Sdk` + `xunit.runner.visualstudio` (shared in `tests/TestProjects.props`) |

Versions for all NuGet packages live in root `Directory.Packages.props` (central package management).

Root `global.json`:

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

Run tests:

```powershell
dotnet test --solution Acme.slnx
```

Test projects are **executables** (`OutputType: Exe`) and can also run via `dotnet run --project tests/...`.

### Cancellation tokens in async tests

xUnit v3 analyzers enforce **xUnit1051**: pass `TestContext.Current.CancellationToken` to any async API that accepts `CancellationToken` (HTTP clients, `IRequestDispatcher.SendAsync`, etc.). See [Testing instructions](../instructions/testing.md#xunit-analyzer-warnings).

## Consequences

### Positive

- Native `dotnet test` on .NET 10 without VSTest shim.
- Faster test runs; modern filter syntax (`--filter-class`, etc.).
- Aligns with future direction of .NET testing.

### Negative

- Different from older xUnit v2 + VSTest tutorials.
- Some IDE integrations still assume VSTest; we include VSTest adapter packages for Test Explorer while CLI uses MTP.
- API integration tests use `Microsoft.AspNetCore.Mvc.Testing` alongside xUnit v3 (compatible).

### Update — xUnit package major 3.x → 4.x

`xunit.v3.mtp-v2` moved 3.2.2 → **4.0.1** (and `xunit.runner.visualstudio` 3.1.5 → **4.0.0**). This is a
*package* major, not a framework change: the id, the framework (xUnit **v3**) and MTP v2 are unchanged, and
the bump needed no source or `xunit.runner.json` edits.

Native AOT for test projects was evaluated at the same time and **rejected**: `PublishAot` emits trim/AOT
warnings from the xUnit runner assemblies and the published binary fails at startup, because the in-process
runner reads `Assembly.Location` (always empty under AOT). It also requires dropping the VSTest adapters
this ADR keeps for Test Explorer. Revisit if upstream ships AOT-clean runner assemblies.

### References

- `tests/Acme.Modules.<M>.Domain.Tests/`
- `tests/Acme.Api.Tests/`
- [Instructions: Testing](../instructions/testing.md)
