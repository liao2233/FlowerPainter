import {test,expect,Page} from "@playwright/test";
import {open,drawPolygon,FIELD,panelShot,waitPlanned} from "./helpers";

async function flowers(page:Page,width=1920,height=1080){
    await open(page,{real:true,width,height});
    await page.waitForFunction(()=>(window as any).pm.backend.hostUp===true);
    await drawPolygon(page,FIELD);await page.getByTestId("primary").click();
    await expect(page.getByTestId("flower-mode-carpet")).toBeVisible();
}
test("standalone catalog and carpet choices",async({page})=>{
    await flowers(page);
    expect(await page.evaluate(()=>(window as any).pm.backend.layers.map((l:any)=>l.id))).toEqual(["floral"]);
    await expect(page.getByTestId("make-groups")).toHaveCount(0);
    await page.getByTestId("carpet-islands").click();
    await waitPlanned(page);
    expect(await page.evaluate(()=>(window as any).pm.backend.root.values.shape)).toBe("islands");
    await panelShot(page,"flowers-carpet");
});
test("patterns retain independent width when resized and curves accept nodes",async({page})=>{
    await flowers(page);await page.getByTestId("flower-mode-compose").click();
    for(const kind of ["ring","heart","petal","curve"])await page.getByTestId("motif-add-"+kind).click();
    await expect(page.getByTestId("motif-select-3")).toBeVisible();
    const before=await page.evaluate(()=>(window as any).pm.backend.root.values.motifs[3].split("|"));
    await page.getByTestId("motif-size-input").fill("90");await page.getByTestId("motif-size-input").press("Enter");
    let after=await page.evaluate(()=>(window as any).pm.backend.root.values.motifs[3].split("|"));
    expect(Number(after[5])).toBe(90);expect(after[7]).toBe(before[7]);
    await page.getByTestId("motif-add-node").click();
    after=await page.evaluate(()=>(window as any).pm.backend.root.values.motifs[3].split("|"));
    expect(after[9].split(";").length).toBe(4);
    await panelShot(page,"flowers-curve-controls");
});
test("region fill choices and narrow panel remain usable",async({page})=>{
    await flowers(page,1280,720);await page.getByTestId("flower-mode-compose").click();
    for(const style of ["single","flow","islands","empty"]){
        await page.getByTestId("fill-style-"+style).click();
        await expect(page.getByTestId("fill-style-"+style)).toHaveAttribute("aria-pressed","true");
    }
    await page.getByTestId("fill-pick-region").click();
    await expect(page.getByTestId("fill-pick-region")).toHaveAttribute("aria-pressed","true");
    expect(await page.getByTestId("placemaker-panel").count()).toBe(0);
    const overflow=await page.getByTestId("flowerpainter-panel").evaluate(e=>e.scrollWidth>e.clientWidth+1);
    expect(overflow).toBe(false);await panelShot(page,"flowers-narrow-regions");
});

test("curve flowers and fill flowers remain independent", async ({page}) => {
    await flowers(page); await page.getByTestId("flower-mode-compose").click();
    await expect(page.getByTestId("curve-materials")).toBeVisible();
    await page.getByTestId("assetmulti-add-curvePalette").click();
    await page.getByTestId("asset-sample-a").click();
    await page.keyboard.press("Escape");
    await page.getByTestId("motif-add-curve").click();
    await page.getByTestId("assetmulti-add-flowerPalette").click();
    await page.getByTestId("asset-grid").getByTestId("asset-sample-b").first().click();
    await page.keyboard.press("Escape");
    const values = await page.evaluate(() => (window as any).pm.backend.root.values);
    expect(values.curvePalette).toEqual(["sample-a"]);
    expect(values.flowerPalette).toEqual(["sample-b"]);
    expect(decodeURIComponent(values.motifs[0].split("|")[8])).toBe("");
    await page.getByTestId("motif-flower").click();
    await page.getByTestId("asset-grid").getByTestId("asset-sample-b").first().click();
    await expect(page.getByTestId("motif-flower")).toContainText("Sample B");
    const after = await page.evaluate(() => (window as any).pm.backend.root.values);
    expect(decodeURIComponent(after.motifs[0].split("|")[8])).toBe("sample-b");
    expect(after.curvePalette).toEqual(["sample-a"]);
    await panelShot(page,"flowers-independent-materials");
});

test("overlap is editable in both planting modes and independent of prop size", async ({page}) => {
    await flowers(page);
    await expect(page.getByTestId("slider-overlap-input")).toHaveValue("50");
    const size = await page.getByTestId("slider-flowerScale-input").inputValue();
    await page.getByTestId("slider-overlap-input").fill("70");
    await page.getByTestId("slider-overlap-input").press("Enter");
    await expect(page.getByTestId("slider-flowerScale-input")).toHaveValue(size);
    expect(await page.evaluate(() => (window as any).pm.backend.root.values.overlap)).toBe(70);
    await page.getByTestId("flower-mode-compose").click();
    await expect(page.getByTestId("slider-overlap-input")).toHaveValue("70");
    await page.getByTestId("motif-add-curve").click();
    await page.getByTestId("slider-overlap-input").fill("90");
    await page.getByTestId("slider-overlap-input").press("Enter");
    expect(await page.evaluate(() => (window as any).pm.backend.root.values.overlap)).toBe(90);
    await expect(page.getByTestId("slider-flowerScale-input")).toHaveValue(size);
    await panelShot(page,"flowers-overlap");
});

test("subdivision mode draws boundaries and offers per-region filling", async ({page}) => {
    await flowers(page);
    await page.getByTestId("flower-mode-partition").click();
    await expect(page.getByTestId("flower-mode-partition")).toHaveAttribute("aria-pressed","true");
    await page.getByTestId("motif-add-ring").click();
    await expect(page.getByTestId("motif-select-0")).toBeVisible();
    await expect(page.getByTestId("motif-flower")).toHaveCount(0);
    await expect(page.getByTestId("motif-width-input")).toHaveCount(0);
    await expect(page.getByTestId("motif-fill-gaps")).toHaveCount(0);
    await expect(page.getByTestId("curve-materials")).toHaveCount(0);
    await expect(page.getByTestId("flower-regions")).toBeVisible();
    await page.getByTestId("fill-pick-region").click();
    await expect(page.getByTestId("fill-pick-region")).toHaveAttribute("aria-pressed","true");
    await page.getByTestId("motif-add-curve").click();
    expect(await page.evaluate(() => (window as any).pm.backend.floralDrawing)).toBe(true);
    await expect(page.getByTestId("motif-close-curve")).toBeDisabled();
    for (let i=0;i<3;i++) await page.getByTestId("motif-add-node").click();
    await expect(page.getByTestId("motif-close-curve")).toBeEnabled();
    await page.getByTestId("motif-close-curve").click();
    const values = await page.evaluate(() => (window as any).pm.backend.root.values);
    expect(values.shape).toBe("partition");
    const points=values.motifs[1].split("|")[9].split(";");
    expect(points[0]).toBe(points[points.length-1]);
    await panelShot(page,"flowers-partition");
    await page.getByTestId("flower-mode-compose").click();
    await expect(page.getByTestId("motif-flower")).toBeVisible();
    await expect(page.getByTestId("motif-width-input")).toBeVisible();
});
