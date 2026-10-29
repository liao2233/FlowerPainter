import React from "react";
import {StepView, ToolState} from "../contract";
import {act} from "../lib/bindings";
import {useT} from "../lib/l10n";
import {Btn, Section} from "./common";
import {usePicker, useAssetList} from "./picker";
import s from "./floral.module.scss";

function readFill(value?: string) {
    const fields = value?.split("|") || [];
    const flower = fields[4] ? decodeURIComponent(fields[4]) : "";
    return {style: fields[3] || "single", flowers: fields[0] === "v2" && fields[5] ? fields[5].split(",").map(decodeURIComponent) : flower ? [flower] : []};
}

export interface RegionDraft { source:string; style:string; flowers:string[] }
export type RegionDrafts=Record<number,RegionDraft>;
export const FloralPartitionFill = ({step, state,drafts,setDrafts,context}: {step: StepView; state: ToolState; drafts:RegionDrafts;setDrafts:React.Dispatch<React.SetStateAction<RegionDrafts>>;context:string}) => {
    const {t} = useT();
    const partitions = state.floralPartitions;
    const fills = (step.values.fills || []) as string[];
    const selected = partitions?.regions.find(r => r.id === partitions.selected);
    return <>
        <Section title={t("FlowerPainter.UI.Partitions.Select", "2 · Select a region")} testId="flower-regions">
            <div className={s.hint}>{t("FlowerPainter.UI.Partitions.SelectTip", "Click a region on the map or in the list. Selecting it does not plant flowers.")}</div>
            <div className={s.buttons}>
                {partitions?.regions.map(r => <Btn key={r.id} selected={r.id === selected?.id} label={`${t("FlowerPainter.UI.Flowers.Region", "Region {0}", [r.id])} · ${r.fillIndex < 0 || readFill(fills[r.fillIndex]).style === "empty" ? t("FlowerPainter.UI.Partitions.Unfilled", "Empty") : t("FlowerPainter.UI.Partitions.Filled", "Filled")}`} onClick={() => act.selectFloralRegion(r.id)} testId={`partition-select-${r.id}`} />)}
            </div>
            {!partitions?.regions.length ? <div className={s.hint}>{t("FlowerPainter.UI.Partitions.Computing", "Calculating regions…")}</div> : null}
            <Btn label={t("FlowerPainter.UI.Partitions.Back", "Back to boundaries")} onClick={() => act.selectFloralPattern(step.path, -1)} testId="partition-edit-boundaries" />
        </Section>
        {selected ? <RegionSettings key={`${selected.id}:${fills[selected.fillIndex] || ""}`} draft={drafts[selected.id]} source={context+"|"+(fills[selected.fillIndex]||"")} onDraft={draft=>setDrafts(prev=>({...prev,[selected.id]:draft}))} id={selected.id} fill={fills[selected.fillIndex]} fallback={step.values.flowerPalette as string[]} /> : <div className={s.hint} data-testid="partition-select-hint">{t("FlowerPainter.UI.Partitions.SelectFirst", "Select a region to choose its props and fill style.")}</div>}
    </>;
};

const RegionSettings = ({id, fill, fallback,draft,source,onDraft}: {id: number; fill?: string; fallback?: string[]; draft?:RegionDraft;source:string;onDraft:(draft:RegionDraft)=>void}) => {
    const {t} = useT(); const picker = usePicker(); const assets = useAssetList("flower");
    const saved = readFill(fill);
    const savedFlowers=saved.flowers.length?saved.flowers:fill && saved.style!=="single"?fallback||[]:[];
    const current=draft?.source===source?draft:{source,style:saved.style,flowers:savedFlowers};
    const {style,flowers}=current;
    const setStyle=(style:string)=>onDraft({...current,style});
    const setFlowers=(flowers:string[])=>onDraft({...current,flowers});
    const dirty=style!==saved.style || JSON.stringify(flowers)!==JSON.stringify(savedFlowers);
    const labels: Record<string,string> = {single:t("FlowerPainter.UI.Flowers.Single", "One flower"), flow:t("FlowerPainter.UI.Flowers.Flow", "Flowing colours"), islands:t("FlowerPainter.UI.Flowers.Islands", "Colour islands"), empty:t("FlowerPainter.UI.Flowers.Empty", "Leave empty")};
    return <Section title={t("FlowerPainter.UI.Partitions.Settings", "3 · Fill region {0}", [id])} testId="partition-settings">
        <div className={s.hint}>{t("FlowerPainter.UI.Partitions.OwnSettings", "These props and this style apply only to the selected region. Press Fill region to apply.")}</div>
        {dirty?<div role="status" data-testid="partition-unsaved">{t("FlowerPainter.Workflow.Unapplied","Not applied — your choices are kept when switching regions.")}</div>:null}
        <div className={s.buttons}>{Object.keys(labels).map(k => <Btn key={k} label={labels[k]} selected={style === k} onClick={() => setStyle(k)} testId={`fill-style-${k}`} />)}</div>
        {style !== "empty" ? <>
            <Btn label={t("FlowerPainter.UI.Partitions.Props", "Choose props for this region")} onClick={() => picker.open({category:"flower", mode:style === "single" ? "single" : "multi", selected:style === "single" ? flowers.slice(0,1) : flowers, title:t("FlowerPainter.UI.Partitions.Props", "Choose props for this region"), allowAutomatic:false, onPick:setFlowers})} testId="fill-flower" />
            <div className={s.hint} data-testid="partition-props">{(style === "single" ? flowers.slice(0,1) : flowers).map(f => assets?.find(a => a.id === f)?.name || f).join(" · ") || t("FlowerPainter.UI.Partitions.NoProps", "No props selected")}</div>
        </> : null}
        <Btn variant="accent" label={style === "empty" ? t("FlowerPainter.UI.Partitions.Clear", "Clear this region") : t("FlowerPainter.UI.Partitions.Apply", "Fill this region")} disabled={style !== "empty" && flowers.length === 0} onClick={() => act.fillFloralRegion(style, style === "single" ? flowers.slice(0,1) : style === "empty" ? [] : flowers)} testId="partition-apply" />
    </Section>;
};
