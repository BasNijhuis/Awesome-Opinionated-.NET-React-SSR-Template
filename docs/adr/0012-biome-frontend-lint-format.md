# ADR-0012: Frontend linting & formatting with Biome

- **Status:** Accepted
- **Date:** 2026-06-20

## Context

The .NET side has CSharpier for formatting ([ADR-0011](./0011-build-hygiene-formatting-lockfiles.md)), but the React Router SSR frontend (`src/acme-web`) had no enforced formatter or linter — only `tsc` type-checking. We want one fast tool for both formatting and linting TS/TSX, consistent with the repo's "tooling over conventions" stance.

## Decision

Adopt **Biome** for the frontend:

- `@biomejs/biome` (catalog devDependency) + a single **root** `biome.json` covering all JS workspace members (`src/acme-web/**`, `scripts/**`, `tests/e2e/**`).
- Scripts: root `pnpm run lint` (`biome check .`) and `pnpm run format` (`biome check --write .`) lint all members in one pass.
- The Biome VS Code extension auto-discovers the binary from the hoisted root `node_modules` — no version-pinned `biome.lsp.bin` and no postinstall path-fix hack.
- **Respect `.gitignore`** (`vcs.useIgnoreFile`) and additionally exclude the web's generated files:
  - `app/lib/api/generated/` — the generated API client ([ADR-0010](./0010-openapi-contract-generated-clients.md)),
  - `.react-router/` — generated route types,
  - `**/*.css` — Biome's CSS parser doesn't understand Tailwind 4 at-rules ([ADR-0004](./0004-react-router-ssr-pnpm.md)); Tailwind owns the CSS.
- Recommended rules on, with two project-fit adjustments:
  - `style/noNonNullAssertion`: **off** — non-null assertions are used deliberately in SSR loaders/actions.
  - `correctness/useExhaustiveDependencies`: **warn** — some effects intentionally omit/extend deps.

CSharpier remains the C# formatter; Biome covers only the frontend.

## Consequences

### Positive

- One fast tool for frontend format + lint; consistent style enforced.
- Generated artifacts and Tailwind CSS are excluded, so the linter only sees authored code.

### Negative

- A second formatter to run/keep in CI (`pnpm run lint` / `pnpm run format`), separate from CSharpier.
- Biome can't lint/format the Tailwind CSS; that stays outside the tool.
- Rule tuning is a living decision — adjust severities in `biome.json` as the codebase grows.

## Related

- [ADR-0011: Build hygiene (CSharpier, lock files, TFM)](./0011-build-hygiene-formatting-lockfiles.md)
- [ADR-0004: React Router + pnpm](./0004-react-router-ssr-pnpm.md)
- [instructions/frontend-development.md](../instructions/frontend-development.md)
