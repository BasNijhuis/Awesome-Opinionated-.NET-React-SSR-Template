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
| Package | `xunit.v3.mtp-v2` **3.2.2** |
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

### References

- `tests/Acme.Modules.<M>.Domain.Tests/`
- `tests/Acme.Api.Tests/`
- [Instructions: Testing](../instructions/testing.md)
