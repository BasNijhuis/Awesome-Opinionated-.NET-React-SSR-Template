# GEMINI.md

This repo is the **Awesome Opinionated Template** — a .NET Aspire modular monolith (.NET 10, C#) with
a React Router SSR frontend (pnpm, TypeScript). The architecture and its rules live in
[`docs/adr/`](docs/adr/README.md) and [`docs/instructions/`](docs/instructions/README.md); boundaries
are enforced by Roslyn analyzers and NetArchTest, so read those before changing structure.

## Build / test (quick reference)

- Backend: `dotnet build *.slnx` · `dotnet test`
- Frontend: `cd src/Services/*-web && pnpm install && pnpm run build && pnpm test`
- Format: `dotnet csharpier .` · `pnpm run format`

## Setting up a new project from this template

When the user wants to **initialize / bootstrap / rebrand** this template into their own project,
follow the canonical, step-by-step instructions in **[`docs/template-setup.md`](docs/template-setup.md)**
and execute them in order. The split:

1. **Mechanical rename** — run the cross-platform script `setup.cs` (a .NET 10 file-based app for
   macOS/Linux/Windows): `dotnet run setup.cs` (interactive), `--dry-run` to preview, `--yes` + flags
   for non-interactive. It renames the namespace/assembly/`.slnx` prefix, the analyzer diagnostic
   prefix, the frontend folder, the Aspire resource names, the database, and the title. Don't do this
   by hand.
2. **Judgement work** (yours) — scaffold real modules for the user's domain, regenerate the EF
   migrations + OpenAPI/TS client, rewrite the README, verify the build.

`docs/template-setup.md` is the single source of truth, shared with the Claude/Copilot/Codex/Cursor
entrypoints — do not reinterpret it here.
