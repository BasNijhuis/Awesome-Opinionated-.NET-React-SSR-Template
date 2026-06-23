# Instructions

Day-to-day guides for working in this template. For architectural rationale, see [ADRs](../adr/README.md).

| Guide | When to use |
|-------|-------------|
| [Local development](./local-development.md) | First run, daily startup, stopping the stack |
| [Debugging](./debugging.md) | F5 in Cursor/VS Code, breakpoints, Aspire dashboard |
| [Testing](./testing.md) | Running tests, **required when finishing a task**, AAA structure |
| [Project structure](./project-structure.md) | Where code lives, dependency rules |
| [Backend development](./backend-development.md) | Adding API features, domain rules |
| [Frontend development](./frontend-development.md) | Routes, SSR, pnpm, internal API client |

## Quick reference

```powershell
# Full stack (requires Docker)
aspire run

# Or without Aspire CLI
dotnet run --project src/Aspire/Acme.AppHost

# Build & test
dotnet build Acme.slnx
dotnet test --solution Acme.slnx

# Frontend only (standalone)
cd src/Services/acme-web
pnpm run dev

# Trust HTTPS dev certs (once per machine)
dotnet dev-certs https --trust
```

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| .NET SDK | 10.x | `dotnet --version` |
| Docker Desktop | latest | Required for Postgres |
| Node.js | 22+ | Frontend |
| pnpm | 11+ | `corepack enable` |
| Aspire CLI | 13.4.5 | `dotnet tool install -g aspire.cli --version 13.4.5` |

## Related

- [README.md](../../README.md) — project intro
