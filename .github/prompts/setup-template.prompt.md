---
agent: agent
description: Initialize a new project from the Awesome Opinionated Template — rename + scaffold modules + verify.
---

# Set up a project from this template (GitHub Copilot)

You are bootstrapping a fresh project from the Awesome Opinionated Template (.NET Aspire modular
monolith + React Router SSR).

The work splits in two:

1. **Mechanical rename** — owned by the cross-platform script `setup.cs` (.NET 10 file-based app;
   macOS/Linux/Windows). Run it (`dotnet run setup.cs`, or `./setup.sh` / `./setup.ps1`); don't
   rename by hand. `--dry-run` previews, `--yes` + flags run it non-interactively.
2. **Judgement work** — owned by you: scaffold real modules for the user's domain, regenerate the
   EF migrations + OpenAPI/TS client, rewrite the README, verify the build.

**Follow the canonical, step-by-step instructions in [`docs/template-setup.md`](../../docs/template-setup.md)**
and execute its steps in order. It is the single source of truth — do not reinterpret it here.
