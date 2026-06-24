// Post-processes the generated `app/lib/api/generated/types.gen.ts` in place so DTO/contract types are
// `z.infer` of the generated Zod schemas (#17) — making the Zod schemas the single source of truth for
// app-facing types, so app code can import them straight from `types.gen.ts` and never sees a hand-static
// DTO. The per-operation wrapper types the SDK depends on (…Data / …Errors / …Responses, which the Zod
// plugin does not emit) are kept verbatim, so the auto-generated `sdk.gen.ts` and `index.ts` need no edits.
// Run via `tsx` as the last step of `api:generate` (the `gen-api-types` script); output is git-ignored
// and must not be hand-edited (ADR-0010). No runtime deps — reads the schema names from `zod.gen.ts`.
import { readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

// Lives in the repo-root `scripts/` workspace member; targets the web app's generated client dir.
const generatedDir = resolve(
  dirname(fileURLToPath(import.meta.url)),
  "../src/Services/acme-web/app/lib/api/generated",
);

// Every exported `z<Name>` schema → contract type `<Name> = z.infer<typeof schemas.z<Name>>`.
const zodSource = readFileSync(resolve(generatedDir, "zod.gen.ts"), "utf8");
const zodNames = new Set(
  [...zodSource.matchAll(/^export const (z[A-Z]\w*)\b/gm)].map((match) => match[1]),
);

// Net brace/bracket/paren depth of a line. Braces embedded in hey-api string/template literals are always
// balanced within their own line (`'/api/widgets/{id}'`, `` `${string}://${string}` ``), so they net
// to 0 and don't confuse the multi-line declaration scanner below.
const depthDelta = (line: string) => {
  let depth = 0;
  for (const ch of line) {
    if (ch === "{" || ch === "[" || ch === "(") depth++;
    else if (ch === "}" || ch === "]" || ch === ")") depth--;
  }
  return depth;
};

const typesPath = resolve(generatedDir, "types.gen.ts");
const lines = readFileSync(typesPath, "utf8").split("\n");

// Walk the file declaration by declaration. Any `export type X` that has a `zX` schema becomes a
// `z.infer` alias in place; everything else (the wrapper types, `ClientOptions`) is kept verbatim.
const kept = [];
const replaced = new Set();
for (let i = 0; i < lines.length; i++) {
  const start = lines[i].match(/^export type (\w+) = /);
  if (!start) {
    kept.push(lines[i]);
    continue;
  }

  // Accumulate the (possibly multi-line) declaration until the brace depth returns to 0 on a `;`.
  let decl = lines[i];
  let depth = depthDelta(lines[i]);
  while (!(depth === 0 && /;\s*$/.test(decl)) && i + 1 < lines.length) {
    i++;
    decl += `\n${lines[i]}`;
    depth += depthDelta(lines[i]);
  }

  const name = start[1];
  if (zodNames.has(`z${name}`)) {
    kept.push(`export type ${name} = z.infer<typeof schemas.z${name}>;`);
    replaced.add(name);
  } else {
    kept.push(decl);
  }
}

// Drop hey-api's banner (and, on a re-run, our own header + imports) so we can prepend a fresh one.
while (kept.length && !/^export\s/.test(kept[0])) kept.shift();

// Contract types the Zod plugin emits but the TypeScript plugin doesn't (per-operation request/response
// breakdowns like `…Body` / `…Path`) — appended so the app's re-exported surface stays complete.
const extras = [...zodNames]
  .map((schema) => schema.slice(1))
  .filter((name) => !replaced.has(name))
  .sort();

const header = [
  "// AUTO-GENERATED: @hey-api/openapi-ts output, post-processed by scripts/generate-api-types.ts.",
  "// DTO/contract types are `z.infer` of the generated Zod schemas (#17) — the schemas are the single",
  "// source of truth. The per-operation wrapper types (…Data / …Errors / …Responses) are kept as-is",
  "// because the SDK imports them. Do not hand-edit (ADR-0010).",
  'import type { z } from "zod";',
  'import * as schemas from "./zod.gen";',
];

const parts = [header.join("\n"), kept.join("\n").trim()];
if (extras.length) {
  parts.push(
    [
      "// Per-operation request/response contract types (no @hey-api/typescript-plugin counterpart).",
      ...extras.map((name) => `export type ${name} = z.infer<typeof schemas.z${name}>;`),
    ].join("\n"),
  );
}

writeFileSync(typesPath, `${parts.join("\n\n")}\n`);

console.log(
  `Rewrote types.gen.ts: ${replaced.size} inlined, ${extras.length} appended as z.infer contract types.`,
);
