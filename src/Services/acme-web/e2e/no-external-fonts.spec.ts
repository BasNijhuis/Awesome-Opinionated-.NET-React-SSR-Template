import { expect, test } from "@playwright/test";

// Regression guard: fonts are self-hosted (#50), so no page may reach out to the Google Fonts CDN.
// A single navigation is enough — the font <link>s live in the shared root layout. We also confirm
// the brand display face (Cinzel) actually applies, so "no external requests" can't pass by simply
// dropping the fonts.
test("loads no fonts from the Google Fonts CDN and still renders Cinzel", async ({ page }) => {
  const externalFontRequests: string[] = [];
  page.on("request", (request) => {
    const url = request.url();
    if (url.includes("fonts.googleapis.com") || url.includes("fonts.gstatic.com")) {
      externalFontRequests.push(url);
    }
  });

  await page.goto("/");
  // Let any (unwanted) late font fetches fire before asserting.
  await page.waitForLoadState("networkidle");

  expect(externalFontRequests).toEqual([]);

  // The display face is applied to headings via the `font-display` utility (var(--font-display)).
  const heading = page.getByRole("heading", { level: 1 }).first();
  await expect(heading).toBeVisible();
  const fontFamily = await heading.evaluate((el) => getComputedStyle(el).fontFamily);
  expect(fontFamily).toContain("Cinzel");
});
