<!--
PR template. Keep it short and grounded in the diff.
Use "Closes #<n>" so merging auto-closes the implemented issue.
-->

## Summary

<!-- 1–3 sentences: what this PR does and why. -->

Closes #

## What changed

<!-- Grouped bullets by area: Domain / Application / Infrastructure / API / Web (SSR) / Tests / docs / build. -->

-

## Verification

<!-- Report actual results. Remove rows that don't apply; don't claim a gate you didn't run. -->

- [ ] `dotnet build Acme.slnx` — clean
- [ ] `dotnet test --solution Acme.slnx` — green
- [ ] `dotnet csharpier check .` — formatted
- [ ] `pnpm run typecheck` — passes (if the frontend changed)

## Notes for reviewers

<!-- Anything non-obvious: large mechanical diffs, follow-ups, out-of-scope items, ADR links. -->

-
