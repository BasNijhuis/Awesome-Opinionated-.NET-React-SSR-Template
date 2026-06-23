# Copilot instructions

This repo is the **Awesome Opinionated Template** — a .NET Aspire modular monolith with a React
Router SSR frontend. The architecture and its rules are documented in
[`docs/adr/`](../docs/adr/README.md) and [`docs/instructions/`](../docs/instructions/README.md);
follow them. Boundaries are enforced by Roslyn analyzers and NetArchTest, so violating them fails
the build.

## Setting up a new project from this template

If the user wants to **initialize / bootstrap / rebrand** this template into their own project, follow
the canonical instructions in [`docs/template-setup.md`](../docs/template-setup.md). In short: run the
cross-platform rename script (`dotnet run setup.cs`) first, then scaffold real modules for their
domain, regenerate the EF migrations + OpenAPI/TS client, and rewrite the README. The reusable prompt
lives at [`.github/prompts/setup-template.prompt.md`](prompts/setup-template.prompt.md).
