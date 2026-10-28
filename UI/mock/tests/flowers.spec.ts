import {test,expect,Page} from "@playwright/test";
import {open,drawPolygon,FIELD,panelShot,waitPlanned} from "./helpers";

test("flower patches and flags expose their own planting controls",async({page})=>{
    await flowers(page,1280,720);
    await page.getByTestId("planting-patch").click();
    await expect(page.getByTestId("slider-bloomSpacing-input")).toHaveValue("0.28");
    await expect(page.getByTestId("slider-flowerDensity-input")).toHaveValue("4");
    await expect(page.getByTestId("slider-overlap-input")).toHaveCount(0);
    await page.getByTestId("flower-mode-flag").click();
    await expect(page.getByTestId("flag-templates")).toBeVisible();
    await expect(page.getByTestId("place-flag")).toBeVisible();
    await page.evaluate(()=>{const b=(window as any).pm.backend;b.root.values.flagDirect=true;b.publishAll();});
    await expect(page.getByTestId("flag-drag-tip")).toBeVisible();
    await expect(page.getByTestId("place-flag")).toHaveCount(0);
    await expect(page.getByTestId("slider-flagAngle-input")).toBeVisible();
    await page.getByTestId("slider-flowerDensity-input").fill("3");
    await page.getByTestId("slider-flowerDensity-input").press("Enter");
    await expect.poll(()=>page.evaluate(()=>(window as any).pm.backend.root.values.flowerDensity)).toBe(3);
    await expect(page.getByTestId("assetmulti-add-flowerPalette")).toHaveCount(0);
    await expect(page.getByTestId("flower-mode-carpet")).toHaveAttribute("aria-pressed","false");
    await expect(page.getByTestId("flag-list").locator("button")).toHaveCount(271);
    await page.getByTestId("flag-search").fill("韩国");
    await expect(page.getByTestId("flag-kr")).toBeVisible();
    await page.getByTestId("flag-kr").click();
    await expect(page.getByTestId("flag-current")).toContainText("Korea");
    await page.getByTestId("flag-search").fill("");
    await page.getByTestId("flag-group-organizations").click();
    await page.getByTestId("flag-wto").click();
    await expect(page.getByTestId("flag-current")).toContainText("World Trade");
    await expect(page.getByTestId("flag-size-advice")).toContainText("56.0");
    await page.getByTestId("flag-group-themes").click();
    await page.getByTestId("flag-first-aid").click();
    expect(await page.evaluate(()=>(window as any).pm.backend.root.values.flagTemplate)).toBe("first-aid");
    await expect(page.getByTestId("flag-size-advice")).toContainText("11.2");
    const broken=await page.getByTestId("flag-list").locator("img").evaluateAll(images=>images.some(img=>!(img as HTMLImageElement).complete||!(img as HTMLImageElement).naturalWidth));
    expect(broken).toBe(false);
    await panelShot(page,"flower-patch-flag");
    await page.getByTestId("flower-mode-carpet").click();
    await expect(page.getByTestId("planting-patch")).toHaveAttribute("aria-pressed","true");
    await page.getByTestId("planting-props").click();
    await expect(page.getByTestId("slider-overlap-input")).toHaveValue("0");
});

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
    await page.getByTestId("planting-props").click();
    await expect(page.getByTestId("slider-overlap-input")).toHaveValue("0");
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
    await expect(page.getByTestId("flower-regions")).toHaveCount(0);
    await expect(page.getByTestId("fill-flower")).toHaveCount(0);
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

test("partition workflow selects first and applies independent props and styles", async ({page}) => {
    await flowers(page,1280,720);
    await page.getByTestId("flower-mode-partition").click();
    await page.getByTestId("motif-add-ring").click();
    await expect(page.getByTestId("partition-settings")).toHaveCount(0);
    await page.getByTestId("partition-finish-boundaries").click();
    // Geometry selection is exercised by the native suite; supply its view contract here.
    await page.evaluate(()=>{const b=(window as any).pm.backend;b.floralPartitions={selected:-1,regions:[{id:1,area:500,fillIndex:-1},{id:2,area:800,fillIndex:-1}]};b.publishAll();});
    await expect(page.getByTestId("floral-pattern-editor")).toHaveCount(0);
    await expect(page.getByTestId("fill-style-single")).toHaveCount(0);
    await page.getByTestId("partition-select-1").click();
    expect(await page.evaluate(()=>(window as any).pm.backend.root.values.fills||[])).toEqual([]);
    await expect(page.getByTestId("partition-apply")).toBeDisabled();
    await page.getByTestId("fill-flower").click();
    await page.getByTestId("asset-grid").getByTestId("asset-sample-a").first().click();
    expect(await page.evaluate(()=>(window as any).pm.backend.root.values.fills||[])).toEqual([]);
    await page.getByTestId("partition-apply").click();
    const first=await page.evaluate(()=>(window as any).pm.backend.root.values.fills[0]);
    await page.getByTestId("partition-select-2").click();
    await expect(page.getByTestId("partition-props")).not.toContainText("Sample A");
    await page.getByTestId("fill-style-flow").click();
    await page.getByTestId("fill-flower").click();
    for (const id of ["sample-a","sample-b"]) await page.getByTestId("asset-grid").getByTestId("asset-"+id).first().click();
    await page.getByTestId("asset-picker-close").click();
    await page.getByTestId("partition-apply").click();
    const fills=await page.evaluate(()=>(window as any).pm.backend.root.values.fills);
    expect(fills[0]).toBe(first);expect(fills[1].split("|")[3]).toBe("flow");expect(fills[1].split("|")[5]).toBe("sample-a,sample-b");
    await page.getByTestId("partition-select-1").click();
    await expect(page.getByTestId("fill-style-single")).toHaveAttribute("aria-pressed","true");
    await expect(page.getByTestId("partition-props")).toContainText("Sample A");
    await expect(page.getByTestId("slider-flowerDensity-input")).toHaveValue("4");
    await page.getByTestId("planting-props").click();
    await expect(page.getByTestId("slider-overlap-input")).toHaveValue("0");
    expect(await page.getByTestId("flowerpainter-panel").evaluate(e=>e.scrollWidth>e.clientWidth+1)).toBe(false);
    await panelShot(page,"flowers-select-then-fill");
    await page.getByTestId("partition-edit-boundaries").click();
    await expect(page.getByTestId("floral-pattern-editor")).toBeVisible();
    await expect(page.getByTestId("partition-settings")).toHaveCount(0);
});
