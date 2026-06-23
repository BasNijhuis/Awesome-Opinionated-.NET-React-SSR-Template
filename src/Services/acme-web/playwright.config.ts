import { defineConfig, devices } from "@playwright/test";

// Deterministic port the AppHost pins the web to in E2E mode (see AppHost.cs `E2E_WEB_PORT`).
const WEB_PORT = 3111;
const BASE_URL = `http://localhost:${WEB_PORT}`;

// The whole stack is started by Aspire: `dotnet run` on the AppHost brings up Postgres + the API +
// the web's *production* server (built, then `node server.js`, which proxies `/hubs` to the API).
// Playwright targets only the web origin (ADR-0003). The boot is heavy (Docker Postgres + a full
// `react-router build`), hence the generous timeout.
export default defineConfig({
  testDir: "./e2e",
  testMatch: "**/*.spec.ts",
  fullyParallel: false,
  // The happy-path spec drives a full (random, multi-step) round; give it well over the 30s default.
  // CI runners are much slower (2 cores + Aspire-proxied SignalR), so allow more headroom there.
  timeout: process.env.CI ? 180_000 : 60_000,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: 1,
  reporter: [["html", { open: "never" }], ["list"]],
  use: {
    baseURL: BASE_URL,
    trace: "on-first-retry",
    screenshot: "only-on-failure",
    video: "retain-on-failure",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: {
    command: "dotnet run --project ../../Aspire/Acme.AppHost --no-launch-profile",
    url: BASE_URL,
    // A cold Aspire boot is heavy: Postgres container + pnpm install + a full `react-router build`
    // before the production server even starts. Give it room.
    timeout: 600_000,
    reuseExistingServer: !process.env.CI,
    // Stream the AppHost output so a slow/stuck Aspire boot is visible in CI logs (Playwright
    // otherwise hides the webServer output until it errors).
    stdout: "pipe",
    stderr: "pipe",
    env: {
      E2E_WEB_PORT: String(WEB_PORT),
      ASPIRE_ALLOW_UNSECURED_TRANSPORT: "true",
    },
  },
});
