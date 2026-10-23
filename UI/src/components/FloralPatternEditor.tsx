import React from "react";
import { StepView, ToolState } from "../contract";
import { act } from "../lib/bindings";
import { useT } from "../lib/l10n";
import { Btn, Section } from "./common";
import { Thumb, useAssetList, usePicker } from "./picker";
import { Slider } from "../controls/Slider";
import s from "./floral.module.scss";

type Motif = { id: string; kind: string; x: number; z: number; size: number; angle: number; width: number; flower: string; points: number[][] };
const kinds = ["ring", "petal", "heart", "curve"];
export function readMotifs(values: unknown): Motif[] {
    if (!Array.isArray(values)) return [];
    return values.map((value): Motif | null => {
        try {
            const a = String(value).split("|");
            const nums = a.slice(3, 8).map(Number);
            if (!((a.length === 9 && a[0] === "v1") || (a.length === 10 && a[0] === "v2")) || ![...kinds,"wave","disc"].includes(a[2]) || nums.some((n) => !Number.isFinite(n))) return null;
            return { id: decodeURIComponent(a[1]), kind: a[2], x: nums[0], z: nums[1], size: nums[2], angle: nums[3], width: nums[4], flower: decodeURIComponent(a[8]), points: a[9] ? a[9].split(";").map(p => p.split(",").map(Number)) : [] };
        } catch { return null; }
    }).filter((x): x is Motif => x !== null).slice(0, 32);
}
const encode = (m: Motif) => { const a = [m.kind === "curve" ? "v2" : "v1", encodeURIComponent(m.id), m.kind, m.x.toFixed(4), m.z.toFixed(4), m.size.toFixed(4), m.angle.toFixed(4), m.width.toFixed(4), encodeURIComponent(m.flower)]; if(m.kind === "curve") a.push(m.points.map(p=>p.join(",")).join(";")); return a.join("|"); };

export const FloralPatternEditor = ({ step, state }: { step: StepView; state: ToolState }) => {
    const { t } = useT();
    const picker = usePicker();
    const flowers = useAssetList("flower");
    const motifs = readMotifs(step.values.motifs);
    const active = state.floralEdit?.path === step.path ? state.floralEdit.index : -1;
    const selected = motifs[active];
    const kindName = (kind: string) => t("FlowerPainter.Floral.Motif." + kind, kind);
    const commit = (next: Motif[], focus = active) => {
        act.setValue(step.path, "motifs", next.map(encode));
        act.selectFloralPattern(step.path, focus >= next.length ? next.length - 1 : focus);
    };
    const add = (kind: string) => {
        const next = [...motifs, { id: Date.now().toString(36), kind, x: 0, z: 0, size: 40, angle: 0, width: 4, points: kind === "curve" ? [[-0.5,0],[0,-0.25],[0.5,0.25]] : [], flower: "" }];
        commit(next, next.length - 1);
    };
    const change = (patch: Partial<Motif>) => commit(motifs.map((m, i) => i === active ? { ...m, ...patch } : m));
    const span = Math.max(100, Math.ceil(Math.sqrt(state.area.areaM2 || 0) * 2 / 10) * 10);
    const adjust = (key: "x" | "z" | "size" | "angle" | "width", label: string) => {
        const position = key === "x" || key === "z";
        const min = position ? -4000 : key === "size" ? 2 : key === "width" ? 0.5 : -360;
        const max = position ? 4000 : key === "size" ? 2000 : key === "width" ? 100 : 360;
        const reach = Math.min(max, Math.max(span, Math.abs(selected?.[key] ?? 0)));
        return (
        <div className={s.adjust} key={key}>
            <span className={s.adjustLabel}>{label}</span>
            <Slider testId={`motif-${key}`} label={label} value={selected?.[key] ?? 0}
                min={position ? -reach : min} max={key === "size" || position ? reach : max}
                inputMin={min} inputMax={max} step={key === "angle" ? 1 : 0.1} unit={key === "angle" ? "deg" : "m"}
                onPreview={(v) => act.previewValue(step.path, "motifs", motifs.map((m, i) => encode(i === active ? { ...m, [key]: v } : m)))}
                onCommit={(v) => change({ [key]: v })} onEndPreview={() => act.endPreview()} />
        </div>
        );
    };
    const chooseFlower = () => {
        if (!selected) return;
        picker.open({ category: "flower", title: t("FlowerPainter.Floral.UI.MotifFlower", "Pattern flower"), mode: "single",
            selected: selected.flower ? [selected.flower] : [], allowAutomatic: true,
            onPick: (ids) => change({ flower: ids[0] ?? "" }) });
    };
    const item = selected?.flower ? flowers?.find((x) => x.id === selected.flower) : undefined;
    const fill = step.values.fillGaps === true;
    return (
        <Section title={t("FlowerPainter.Floral.UI.Compose", "Arrange patterns")} testId="floral-pattern-editor">
            <div className={s.hint}>{state.floralDrawing ? t("FlowerPainter.UI.Flowers.Drawing", "Click to add curve points. Enter or right-click finishes the curve. You can drag its points afterwards.") : t("FlowerPainter.Floral.UI.ComposeTip", "Place circles or flower shapes, or edit a free curve. Parts outside the frame are clipped.")}</div>
            <div className={s.buttons}>
                {kinds.map((kind) => <Btn key={kind} icon="plus" label={kindName(kind)} onClick={() => add(kind)} disabled={motifs.length >= 32} testId={`motif-add-${kind}`} />)}
            </div>
            {motifs.map((m, i) => (
                <div className={s.patternRow} key={m.id + i}>
                    <Btn label={`${i + 1} · ${kindName(m.kind)}`} selected={i === active} onClick={() => act.selectFloralPattern(step.path, i)} testId={`motif-select-${i}`} />
                    <span className={s.rowHint}>{flowers?.find(f => f.id === m.flower)?.name || m.flower || t("FlowerPainter.Floral.UI.AutoFlower", "From curve flowers")} · {Math.round(m.size)}m</span>
                    <Btn icon="plus" title={t("FlowerPainter.Floral.UI.Duplicate", "Duplicate")} onClick={() => commit([...motifs, { ...m, id: Date.now().toString(36), x: m.x + 3, z: m.z + 3 }], motifs.length)} disabled={motifs.length >= 32} testId={`motif-copy-${i}`} />
                    <Btn icon="close" title={t("FlowerPainter.Floral.UI.Remove", "Remove pattern")} onClick={() => commit(motifs.filter((_, n) => n !== i), -1)} testId={`motif-remove-${i}`} />
                </div>
            ))}
            {selected ? (
                <div className={s.detail} key={selected.id}>
                    <div className={s.hint}>{t("FlowerPainter.Floral.UI.DragHint", "Drag the centre to move, green to resize, orange to rotate, and the lower handle for width. Curve nodes can be dragged; right-click a node to remove it.")}</div>
                    <button type="button" className={s.flower} onClick={chooseFlower} data-testid="motif-flower">
                        {item ? <Thumb item={item} size={24} /> : null}
                        <span>{t("FlowerPainter.Floral.UI.MotifFlower", "Pattern flower")}: {item?.name ?? (selected.flower || t("FlowerPainter.Floral.UI.AutoFlower", "From curve flowers"))}</span>
                    </button>
                    {selected.kind === "curve" ? <div className={s.buttons}><Btn label={t("FlowerPainter.Floral.UI.DrawCurve","Draw a new curve")} onClick={() => { change({points:[]}); act.selectFloralPattern(step.path,active+1000); }} testId="motif-draw-curve" /><Btn label={t("FlowerPainter.Floral.UI.AddNode","Add curve node")} onClick={() => change({points:[...selected.points,selected.points.length ? [selected.points[selected.points.length-1][0]+0.2,selected.points[selected.points.length-1][1]] : [0,0]]})} disabled={selected.points.length >= 64} testId="motif-add-node" /></div> : null}<div className={s.adjustments}>
                        {adjust("size", t("FlowerPainter.Floral.UI.Size", "Size"))}
                        {adjust("angle", t("FlowerPainter.Floral.UI.Angle", "Rotation"))}
                        {adjust("width", t("FlowerPainter.Floral.UI.Width", "Ribbon width"))}
                        {adjust("x", t("FlowerPainter.Floral.UI.OffsetX", "East/west"))}
                        {adjust("z", t("FlowerPainter.Floral.UI.OffsetZ", "North/south"))}
                    </div>
                </div>
            ) : null}
            <div className={s.buttons}>
                <Btn label={t("FlowerPainter.Floral.UI.EditFrame", "Edit frame")} onClick={() => act.selectFloralPattern(step.path, -1)} testId="motif-edit-frame" />
                <Btn variant={fill ? "ghost" : "accent"} label={fill ? t("FlowerPainter.Floral.UI.ClearFill", "Clear gap fill") : t("FlowerPainter.Floral.UI.Fill", "Fill gaps with colours")} onClick={() => { act.selectFloralPattern(step.path, -1); act.setValue(step.path, "fillGaps", !fill); }} testId="motif-fill-gaps" />
                {fill ? <Btn icon="shuffle" label={t("FlowerPainter.Floral.UI.NewFill", "New gap colours")} onClick={() => act.randomize(step.path)} testId="motif-reroll-fill" /> : null}
            </div>
        </Section>
    );
};
