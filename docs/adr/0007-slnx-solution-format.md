# ADR-0007: XML solution file (`.slnx`)

- **Status:** Accepted
- **Date:** 2026-06-19

## Context

.NET 10 introduces the **XML solution format** (`.slnx`) as the modern replacement for the legacy `.sln` text format. New `dotnet new sln` commands on .NET 10 generate `.slnx` by default.

## Decision

Use a single root **`Acme.slnx`** with solution folders:

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/Acme.AppHost/..." />
    ...
  </Folder>
  <Folder Name="/tests/">
    ...
  </Folder>
</Solution>
```

Commands:

```powershell
dotnet build Acme.slnx
dotnet test --solution Acme.slnx
dotnet sln Acme.slnx add path/to/project.csproj
```

Do **not** maintain a parallel legacy `.sln` file.

## Consequences

### Positive

- Cleaner XML structure; easier to merge in git.
- Default for .NET 10 tooling going forward.
- Solution folders (`/src/`, `/tests/`) mirror repo layout.

### Negative

- Older tooling or docs may reference `.sln`—use `--solution` flag for `dotnet test`.
- Some third-party tools may not yet support `.slnx`.

### References

- `Acme.slnx` at repo root
