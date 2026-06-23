import { expect, test } from "@playwright/test";

// Verifies the locale toggle end to end: the home page starts in English, switching to Nederlands
// re-renders the document in Dutch (and sets <html lang>), and switching back restores English.
// The choice is cookie-backed, so it must survive a full reload. The "Greetings" nav link is a good
// probe because it differs between locales (Greetings / Begroetingen).
test("language toggle switches the home page between English and Dutch", async ({ page }) => {
  await page.goto("/");

  // Default locale is English.
  await expect(page.locator("html")).toHaveAttribute("lang", "en");
  await expect(page.getByRole("link", { name: "Greetings" })).toBeVisible();

  // Switch to Dutch via the language toggle.
  await page.getByRole("button", { name: "Nederlands" }).click();

  await expect(page.locator("html")).toHaveAttribute("lang", "nl");
  await expect(page.getByRole("link", { name: "Begroetingen" })).toBeVisible();
  // English nav label is gone.
  await expect(page.getByRole("link", { name: "Greetings" })).toHaveCount(0);

  // The cookie persists the choice across a hard reload.
  await page.reload();
  await expect(page.locator("html")).toHaveAttribute("lang", "nl");
  await expect(page.getByRole("link", { name: "Begroetingen" })).toBeVisible();

  // Switch back to English.
  await page.getByRole("button", { name: "English" }).click();
  await expect(page.locator("html")).toHaveAttribute("lang", "en");
  await expect(page.getByRole("link", { name: "Greetings" })).toBeVisible();
});
