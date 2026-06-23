import { expect, test } from "@playwright/test";

// Proves the cookie-driven locale forwarding end to end: the language toggle sets the locale cookie,
// the SSR root middleware reads it and forwards it as Accept-Language on every API call, and the
// backend (ILocaleProvider) serves the greeting suggestion in that language. Mirrors the bundled
// bilingual bank (greeting-suggestions.json).
const DUTCH = ["Hallo daar!", "Goed je te zien!", "Welkom!", "Fijne dag!", "Gegroet, vriend!"];

test("greeting suggestions follow the cookie locale", async ({ page }) => {
  await page.goto("/greetings");

  // Switch to Dutch — the cookie now drives the locale for subsequent requests.
  await page.getByRole("button", { name: "Nederlands" }).click();
  await expect(page.locator("html")).toHaveAttribute("lang", "nl");

  // The suggestion (served by the backend) is now Dutch, proving the cookie reached the API.
  const suggestion = await page.getByTestId("greeting-suggestion").innerText();
  expect(DUTCH.some((phrase) => suggestion.includes(phrase))).toBe(true);
});
