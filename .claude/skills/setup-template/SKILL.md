---
name: setup-template
description: Initialize a new project from the Awesome Opinionated Template — run the cross-platform rename script (namespace/analyzer/web/db/title), then scaffold real modules for the user's kind of app, regenerate EF migrations + the OpenAPI/TS client, rewrite the README, and verify the build. Use when setting up / bootstrapping / "make this template mine".
---

# Set up a project from this template

You are bootstrapping a fresh project from the Awesome Opinionated Template (a .NET Aspire modular
monolith + React Router SSR frontend).

The work splits in two:

1. **Mechanical rename** — owned by the cross-platform script `setup.cs` (a .NET 10 file-based app;
   runs on macOS/Linux/Windows). It renames the namespace/assembly/`.slnx` prefix, the analyzer
   diagnostic prefix, the frontend folder, the Aspire resource names, the database name, and branding
   strings. Don't do this by hand — run the script (`dotnet run setup.cs`, or `./setup.sh` /
   `./setup.ps1`). Use `--dry-run` to preview, `--yes` + flags for non-interactive.

2. **Judgement work** — owned by you: scaffolding real modules for the user's domain, regenerating
   derived artifacts, rewriting prose, verifying.

**Follow the canonical, step-by-step instructions in [`docs/template-setup.md`](../../../docs/template-setup.md).**
Read that file now and execute its steps in order. It is the single source of truth (shared with the
Copilot/Codex/Cursor/Gemini entrypoints) — do not duplicate or reinterpret it here.
