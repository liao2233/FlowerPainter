import { expect, Page, test } from "@playwright/test";
import { drawPolygon, FIELD, open, panelShot, pickType, waitPlanned } from "./helpers";

async function design(page: Page, width = 1920, height = 1080) {
    await open(page, { width, height });
    await drawPolygon(page, FIELD);
    await page.getByTestId("primary").click();
    await pickType(page, "park");
}

test("the footer explains when the map and estimate need planning", async ({ page }) => {
    await design(page);
    await expect(page.getByTestId("planning-needed")).toContainText("preview this design");
    await waitPlanned(page);
    await expect(page.getByTestId("planning-needed")).toHaveCount(0);
    await page.getByTestId("feature-toggle-playground").click();
    await expect(page.getByTestId("planning-needed")).toContainText("Design changed");
    await expect(page.getByTestId("quick-numbers")).toHaveAttribute("data-stale", "true");
    await panelShot(page, "clarity-changed-design");
    await waitPlanned(page);
    await expect(page.getByTestId("planning-needed")).toHaveCount(0);
    await expect(page.getByTestId("quick-numbers")).toHaveAttribute("data-stale", "false");
});

for (const [width, height] of [[1280, 720], [1920, 1080]]) {
    test(`all entrance types stay reachable at ${width}x${height}`, async ({ page }) => {
        await design(page, width, height);
        await pickType(page, "parking");
        await page.evaluate(() => {
            const b = (window as any).pm.backend;
            b.markerKindsOf = () => Array.from({ length: 10 }, (_, i) => ({ kind: "entrance" + i, label: "Entrance option " + i, icon: "entrance" }));
            b.publishAll();
        });
        const kinds = page.getByTestId("marker-kinds");
        const buttons = kinds.locator("button");
        await expect(buttons).toHaveCount(10);
        expect(await buttons.evaluateAll((els) => {
            const panel = document.querySelector('[data-testid="flowerpainter-panel"]')!.getBoundingClientRect();
            return els.every(e => { const r = e.getBoundingClientRect(); return r.left >= panel.left && r.right <= panel.right; });
        })).toBe(true);
        await page.getByTestId("marker-kind-entrance9").click();
        await expect(page.getByTestId("marker-kind-entrance9")).toHaveAttribute("aria-pressed", "true");
        await panelShot(page, `clarity-entrances-${width}`);
    });

    test(`long English and Chinese reasons remain visible at ${width}x${height}`, async ({ page }) => {
        await design(page, width, height);
        for (const reason of [
            "This design needs a valid entrance beside a road before you can build it",
            "此设计的出入口尚未连接到道路，请先在临近道路的区域边缘添加有效出入口，然后重新开始规划并检查建造结果",
        ]) {
            await page.evaluate((text) => {
                const b = (window as any).pm.backend;
                b.primary = () => ({ action: "toReview", enabled: false, label: { key: "FlowerPainter.UI.Primary.Next" }, reason: { key: text } });
                b.publishAll();
            }, reason);
            const note = page.getByTestId("primary-explanation");
            await expect(note).toHaveText(reason);
            await expect(page.getByTestId("primary")).toHaveAttribute("aria-describedby", "flowerpainter-primary-reason");
            expect(await note.evaluate((e) => {
                const r = e.getBoundingClientRect();
                const panel = document.querySelector('[data-testid="flowerpainter-panel"]')!.getBoundingClientRect();
                return e.scrollHeight <= e.clientHeight + 1 && r.left >= panel.left && r.right <= panel.right;
            })).toBe(true);
        }
        await panelShot(page, `clarity-reason-${width}`);
    });

    test(`permanent consent explains what remains and gates Build at ${width}x${height}`, async ({ page }) => {
        await design(page, width, height);
        await page.getByTestId("preset-pond").click();
        await waitPlanned(page);
        await page.getByTestId("primary").click();
        const ack = page.getByTestId("ack-permanent");
        await expect(ack).toContainText("Allow permanent changes");
        await expect(ack).toContainText("Removing this build will not undo");
        await expect(page.getByTestId("primary")).toHaveAttribute("aria-disabled", "true");
        const box = (await ack.boundingBox())!;
        const button = (await page.getByTestId("primary").boundingBox())!;
        expect(box.y + box.height).toBeLessThan(button.y);
        expect(box.width).toBeGreaterThan(button.width * 0.8);
        await panelShot(page, `clarity-permanent-${width}`);
        await ack.click();
        await expect(page.getByTestId("primary")).toHaveAttribute("aria-disabled", "false");
        await ack.click();
        await expect(page.getByTestId("primary")).toHaveAttribute("aria-disabled", "true");
    });
}
