import React from "react";
import { Catalog, DesignState, ToolState } from "../contract";
import { act } from "../lib/bindings";
import { useT } from "../lib/l10n";
import { Btn, Section } from "../components/common";
import { ControlList } from "../controls/Controls";
import { FloralPatternEditor } from "../components/FloralPatternEditor";
import { FloralPartitionFill } from "../components/FloralPartitionFill";
import { FlagPicker } from "../components/FlagPicker";
import { usePicker, useAssetList } from "../components/picker";
import s from "../components/floral.module.scss";

export const FlowerDesignStage = ({ catalog, design, state }: {catalog: Catalog; design: DesignState; state: ToolState}) => {
    const {t}=useT(); const picker=usePicker(); const flowers=useAssetList("flower");
    const step=design.root; const layer=catalog.layers.find(l=>l.id==="floral");
    if(!step || !layer) return null;
    const partition=step.values.shape==="partition";
    const flag=step.values.shape==="flag";
    const patch=flag || step.values.plantingSystem==="patch";
    const compose=step.values.shape==="collage" || partition;
    const fillingPartitions=partition && state.floralEdit?.index===-2;
    const style=String(step.values.activeFillStyle || "single");
    const flower=String(step.values.activeFillFlower || "");
    const fills=Array.isArray(step.values.fills)?step.values.fills as string[]:[];
    const labels:Record<string,string>={
        flow:t("FlowerPainter.UI.Flowers.Flow","Flowing colours"), islands:t("FlowerPainter.UI.Flowers.Islands","Colour islands"),
        single:t("FlowerPainter.UI.Flowers.Single","One flower"),empty:t("FlowerPainter.UI.Flowers.Empty","Leave empty")
    };
    return <div data-testid="stage-design">
        <Section title={t("FlowerPainter.UI.Flowers.Method","How to plant")}>
            <div className={s.buttons}>
                <Btn label={t("FlowerPainter.UI.Flowers.Carpet","Fill the area")} selected={!compose && !flag} onClick={()=>{act.selectFloralPattern(step.path,-1);act.setValue(step.path,"shape","flow");}} testId="flower-mode-carpet" />
                <Btn label={t("FlowerPainter.UI.Flowers.Partition","Subdivide the frame")} selected={partition} onClick={()=>{act.selectFloralPattern(step.path,-1);act.setValue(step.path,"shape","partition");}} testId="flower-mode-partition" />
                <Btn label={t("FlowerPainter.UI.Flags.Title","Flags")} selected={flag} onClick={()=>{act.selectFloralPattern(step.path,-1);act.setValue(step.path,"shape","flag");}} testId="flower-mode-flag" />
                <Btn label={t("FlowerPainter.UI.Flowers.Compose","Curves and patterns")} selected={compose && !partition} onClick={()=>act.setValue(step.path,"shape","collage")} testId="flower-mode-compose" />
            </div>
            <div className={s.hint}>{t("FlowerPainter.UI.Flowers.Preview","Real flowers update after edits. Only the part inside your frame is planted.")}</div>
        </Section>
        {compose && !partition ? <Section title={t("FlowerPainter.UI.Flowers.CurveMaterials", "Curve flowers")} testId="curve-materials">
            <div className={s.hint}>{t("FlowerPainter.UI.Flowers.CurveMaterialsTip", "Curves use their own flowers, independently of the fill. Select a pattern to override its flower.")}</div>
            <ControlList step={step} layer={layer} state={state} flat only={["curvePalette"]} />
        </Section> : null}
        {flag ? <Section title={t("FlowerPainter.UI.Flags.Template","Flag template")} testId="flag-templates"><FlagPicker step={step}/>{step.values.flagDirect ? <ControlList step={step} layer={layer} state={state} flat only={["flagWidth"]} /> : null}</Section> : !compose ? <Section title={t("FlowerPainter.UI.Flowers.CarpetPattern","Fill pattern")}><div className={s.buttons}>
            {["flow","islands"].map(k=><Btn key={k} selected={step.values.shape===k} label={labels[k]} onClick={()=>act.setValue(step.path,"shape",k)} testId={`carpet-${k}`} />)}
        </div></Section> : !fillingPartitions ? <FloralPatternEditor step={step} state={state} /> : null}
        {partition && !fillingPartitions ? <Btn variant="accent" label={t("FlowerPainter.UI.Partitions.DoneDrawing", "Finish boundaries → select regions")} onClick={()=>act.selectFloralPattern(step.path,-2)} testId="partition-finish-boundaries" /> : null}
        {fillingPartitions ? <FloralPartitionFill step={step} state={state} /> : null}
        {compose && !partition ? <Section title={t("FlowerPainter.UI.Flowers.Regions","Fill individual regions")} testId="flower-regions">
            <div className={s.hint}>{partition ? t("FlowerPainter.UI.Flowers.PartitionFillTip","Choose flowers and click a subdivision. The hovered subdivision is outlined. Unfilled subdivisions stay empty; boundaries do not plant ribbons.") : t("FlowerPainter.UI.Flowers.RegionTip","Choose a fill and flower, then click an empty region on the map. Inside and outside a circle can use different fills.")}</div>
            <div className={s.buttons}>{Object.keys(labels).map(k=><Btn key={k} selected={style===k} label={labels[k]} onClick={()=>act.setValue(step.path,"activeFillStyle",k)} testId={`fill-style-${k}`} />)}</div>
            {style==="single" ? <Btn label={flowers?.find(f=>f.id===flower)?.name || t("FlowerPainter.UI.Flowers.PickFlower","Choose flower")} onClick={()=>picker.open({category:"flower",mode:"single",title:t("FlowerPainter.UI.Flowers.PickFlower","Choose flower"),selected:flower?[flower]:[],allowAutomatic:false,onPick:ids=>act.setValue(step.path,"activeFillFlower",ids[0]||"")})} testId="fill-flower" /> : null}
            <div className={s.buttons}><Btn variant="accent" selected={state.floralEdit?.index===-2} label={t("FlowerPainter.UI.Flowers.PickRegion","Click a region to fill")} onClick={()=>act.selectFloralPattern(step.path,-2)} testId="fill-pick-region" />
            <Btn label={t("FlowerPainter.UI.Flowers.StopRegion","Finish filling")} onClick={()=>act.selectFloralPattern(step.path,-1)} /></div>
            {fills.map((f,i)=><div className={s.patternRow} key={i}><span>{t("FlowerPainter.UI.Flowers.Region","Region {0}",[i+1])} · {labels[f.split("|")[3]]}</span><Btn icon="close" title={t("FlowerPainter.UI.Flowers.RemoveFill","Remove this fill")} onClick={()=>act.setValue(step.path,"fills",fills.filter((_,j)=>i!==j))} testId={`fill-remove-${i}`} /></div>)}
        </Section>:null}
        {!flag && (!partition || fillingPartitions) ? <Section title={t("FlowerPainter.Floral.plantingSystem","Flower models")}><div className={s.buttons}>{["props","patch"].map(k=><Btn key={k} label={t("FlowerPainter.Floral.plantingSystem."+k,k)} selected={(step.values.plantingSystem || "patch")===k} onClick={()=>act.setValue(step.path,"plantingSystem",k)} testId={`planting-${k}`} />)}</div></Section> : null}
        {!partition || fillingPartitions ? <Section title={partition ? t("FlowerPainter.UI.Partitions.Planting", "Planting spacing and size (all regions)") : compose ? t("FlowerPainter.UI.Flowers.FillMaterials","Fill flowers and planting") : t("FlowerPainter.UI.Flowers.Materials","Flowers and planting")}>
            {patch ? <div className={s.hint}>{t("FlowerPainter.UI.Patches.Tip","Flower patches follow the terrain and region boundary. Blossom spacing controls their density; choose the FlowerPainter colour props to set fill colours.")}</div> : null}
            <ControlList step={step} layer={layer} state={state} flat only={[...(!partition && !flag?["flowerPalette"]:[]),...(patch?["bloomSpacing","flowerDensity","patchHeight","flowerViewDistance"]:["spacing","overlap","flowerScale"]),...(!flag && (!compose || partition)?["bandWidth","rotation"]:[])]} />
        </Section> : null}
    </div>;
};
