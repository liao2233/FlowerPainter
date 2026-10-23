import { test, expect } from "@playwright/test";
import { open, drawPolygon, FIELD, pickType } from "./helpers";

test("a design plans only after the button, and committed changes require a new plan", async ({ page }) => {
    await open(page);
    await drawPolygon(page, FIELD);
    await page.getByTestId("primary").click();
    await pickType(page, "plaza");
    await expect(page.getByTestId("primary")).toHaveAttribute("data-action", "startPlanning");
    await page.waitForTimeout(250);
    expect(await page.evaluate(() => (window as any).pm.backend.planning)).toBe(false);
    expect(await page.evaluate(() => (window as any).pm.backend.plan)).toBeNull();
    await page.getByTestId("primary").click();
    await expect(page.getByTestId("primary")).toHaveAttribute("data-action", "toReview");
    await page.evaluate(() => (window as any).pm.backend.onTrigger("randomize", ["0"]));
    await expect(page.getByTestId("primary")).toHaveAttribute("data-action", "startPlanning");
    await page.waitForTimeout(250);
    expect(await page.evaluate(() => (window as any).pm.backend.stale)).toBe(true);
    await page.getByTestId("primary").click();
    await expect(page.getByTestId("primary")).toHaveAttribute("data-action", "toReview");
});

test("browsing assets preserves the committed plan and does not offer a hover preview", async ({ page }) => {
    await open(page);
    await drawPolygon(page, FIELD);
    await page.getByTestId("primary").click();
    await pickType(page, "plaza");
    await page.getByTestId("primary").click();
    await expect(page.getByTestId("primary")).toHaveAttribute("data-action", "toReview");
    const unchanged = await page.evaluate(() => {
        const b = (window as any).pm.backend;
        const original = b.plan;
        const control = b.layer(b.root.layer).controls.find((c: any) => c.kind === "asset");
        b.onTrigger("previewValue", ["0", control.id, JSON.stringify("uncommitted-hover")]);
        return b.plan === original && !b.stale && b.preview.size === 0;
    });
    expect(unchanged).toBe(true);
    await expect(page.getByTestId("primary")).toHaveAttribute("data-action", "toReview");
});
