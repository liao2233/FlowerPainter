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
