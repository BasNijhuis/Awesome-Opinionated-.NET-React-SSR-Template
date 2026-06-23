import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";

// Standalone Vitest config — intentionally omits the reactRouter() Vite plugin (it transforms
// routes and conflicts with the test runner). Unit tests cover pure helpers + prop-driven
// components; SSR loaders/actions and live-API calls are out of scope (E2E). See issue #11.
export default defineConfig({
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./vitest.setup.ts"],
    include: ["app/**/*.test.{ts,tsx}"],
    coverage: {
      // Published as a non-gating CI artifact (#10) — no thresholds, never fails the run.
      provider: "v8",
      reporter: ["text", "cobertura", "html"],
      reportsDirectory: "./coverage",
      include: ["app/**"],
      exclude: ["app/lib/api/generated/**", "**/*.test.{ts,tsx}", "**/*.d.ts"],
    },
  },
  resolve: {
    alias: {
      "~": fileURLToPath(new URL("./app", import.meta.url)),
    },
  },
});
