import { expect, test } from "@playwright/test";

// Proves the headline cross-module flow in the running app: creating a widget and adjusting its
// quantity (a Widgets command) makes the Greetings module record an announcement greeting — the
// domain event crosses modules inside one transaction (ADR-0016).
test("adjusting a widget's quantity announces it as a greeting", async ({ page }) => {
  // Create a widget.
  await page.goto("/widgets");
  await page.getByTestId("widget-name").fill("Sprocket");
  await page.getByTestId("widget-quantity").fill("5");
  await page.getByTestId("widget-submit").click();

  // Adjust its quantity (+1) — this raises the cross-module domain event.
  const firstWidget = page.getByTestId("widget-list").locator("li").first();
  await firstWidget.getByTestId("widget-increment").click();
  await expect(firstWidget).toContainText("Quantity: 6");

  // The Greetings module reacted: a matching announcement greeting now exists.
  await page.goto("/greetings");
  await expect(page.getByText("Widget 'Sprocket' quantity changed from 5 to 6.")).toBeVisible();
});
