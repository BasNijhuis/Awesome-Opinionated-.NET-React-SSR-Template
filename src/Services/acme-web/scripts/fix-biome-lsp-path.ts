// Keeps the version embedded in `.vscode/settings.json`'s `biome.lsp.bin` paths in sync with the
// installed @biomejs/biome. Those paths point at the native platform binary in pnpm's store
// (`.../.pnpm/@biomejs+cli-<arch>@<version>/...`), whose directory name embeds the package version —
// so without this they'd go stale on every Biome upgrade. Runs on postinstall. Edits the file with a
// surgical regex so the JSONC comments survive, and no-ops safely if the file or paths are absent
// (e.g. CI checkouts without `.vscode/`).
import { readFileSync, writeFileSync } from "node:fs";
import { createRequire } from "node:module";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const settingsPath = resolve(here, "../../../../.vscode/settings.json");

try {
  const nodeRequire = createRequire(import.meta.url);
  const { version } = nodeRequire("@biomejs/biome/package.json") as { version: string };

  const original = readFileSync(settingsPath, "utf8");
  // Replace the version segment of each `@biomejs+cli-<arch>@<version>` store path.
  const updated = original.replace(/(@biomejs\+cli-[a-z0-9-]+@)[^/]+/g, `$1${version}`);

  if (updated !== original) {
    writeFileSync(settingsPath, updated);
    console.log(`[fix-biome-lsp-path] Synced biome.lsp.bin paths to Biome ${version}.`);
  }
} catch (error) {
  // Never fail an install over an editor-convenience setting.
  console.warn(
    `[fix-biome-lsp-path] Skipped (${error instanceof Error ? error.message : String(error)}).`,
  );
}
