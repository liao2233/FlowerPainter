import { expect, Page, test } from "@playwright/test";
import { drawPolygon, FIELD, open, panelShot, pickType, unfoldSections, waitPlanned } from "./helpers";

async function setup(page: Page) {
    const errors = await open(page);
    await drawPolygon(page, FIELD);
    await page.getByTestId("primary").click();
    await pickType(page, "parking");
    await expect(page.getByTestId("stage-design")).toBeVisible();
    await unfoldSections(page);
    await page.evaluate(() => { (window as any).pm.triggerLog.length = 0; });
    return errors;
}
const commits = (page: Page) => page.evaluate(() => (window as any).pm.triggerLog.filter((c: any) => c.name === "setValue").map((c: any) => c.args));
const triggers = (page: Page) => page.evaluate(() => (window as any).pm.triggerLog.map((c: any) => c.name));

test("typing commits once on Enter, clamps to bounds, and supports undo", async ({ page }) => {
    const errors = await setup(page);
    const input = page.getByTestId("slider-aisle-input");
    await input.fill("7.4");
    expect(await commits(page)).toEqual([]);
    await input.press("Enter");
    await expect(input).toHaveValue("7.5");
    expect(await commits(page)).toEqual([["0", "aisle", "7.5"]]);
    // Enter must not reach the tool: no build/Primary trigger, and the field lets go of the keyboard by itself
    // (after a beat, so the game's own update never sees the key without hasInputFieldFocus).
    await expect(input).not.toBeFocused();
    expect(await triggers(page)).toEqual(["setValue"]);
    await page.evaluate(() => (window as any).pm.backend.onTrigger("undo", []));
    await expect(input).toHaveValue("6.0");
    await input.fill("999");
    await input.press("Tab");
    await expect(input).toHaveValue("8.0");
    await expect(page.getByTestId("stage-design")).toBeVisible();
    await panelShot(page, "numeric-slider-input");
    expect(errors).toEqual([]);
});

test("Escape cancels and invalid or empty input never changes the plan", async ({ page }) => {
    const errors = await setup(page);
    const input = page.getByTestId("slider-aisle-input");
    await input.fill("7.5");
    await input.press("Escape");
    await expect(input).toHaveValue("6.0");
    await expect(input).not.toBeFocused();
    await expect(page.getByTestId("flowerpainter-panel")).toBeVisible();
    for (const text of ["", "-", "abc", "Infinity"]) {
        await input.fill(text);
        await input.press("Enter");
        await expect(input).toHaveValue("6.0");
    }
    expect(await commits(page)).toEqual([]);
    expect(await triggers(page)).toEqual([]);
    await input.fill("6,5");
    await input.press("Enter");
    await expect(input).toHaveValue("6.5");
    expect(await commits(page)).toEqual([["0", "aisle", "6.5"]]);
});

test("dragging previews live, releases once and keeps the editable number in sync", async ({ page }) => {
    await setup(page);
    const track = page.getByTestId("slider-aisle-track");
    await track.scrollIntoViewIfNeeded();
    const box = (await track.boundingBox())!;
    await page.mouse.move(box.x + box.width * 0.3, box.y + box.height / 2);
    await page.mouse.down();
    await page.mouse.move(box.x + box.width * 0.85, box.y + box.height / 2, { steps: 5 });
    await expect(page.getByTestId("slider-aisle-input")).toHaveValue("7.5");
    expect(await commits(page)).toEqual([]);
    await page.mouse.up();
    await page.waitForTimeout(100);
    expect(await commits(page)).toEqual([["0", "aisle", "7.5"]]);
    expect(await page.evaluate(() => (window as any).pm.backend.preview.size)).toBe(0);
    await page.evaluate(() => (window as any).pm.backend.onTrigger("undo", []));
    await expect(page.getByTestId("slider-aisle-input")).toHaveValue("6.0");
});
