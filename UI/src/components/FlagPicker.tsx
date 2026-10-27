import React, {useState} from "react";
import {StepView} from "../contract";
import {act, useSummary} from "../lib/bindings";
import {useT} from "../lib/l10n";
import {Btn} from "./common";
import {flags} from "../flags/catalog";
import s from "./floral.module.scss";

export const FlagPicker=({step}:{step:StepView})=>{
    const {t,tl}=useT();const summary=useSummary();
    const [query,setQuery]=useState("");const [group,setGroup]=useState("all");
    const current=flags.find(f=>f.id===step.values.flagTemplate)||flags.find(f=>f.id==="china")!;
    const groups:Record<string,string>={all:t("FlowerPainter.UI.Flags.All","All"),asia:t("FlowerPainter.UI.Flags.Asia","Asia"),europe:t("FlowerPainter.UI.Flags.Europe","Europe"),americas:t("FlowerPainter.UI.Flags.Americas","Americas"),oceania:t("FlowerPainter.UI.Flags.Oceania","Oceania"),africa:t("FlowerPainter.UI.Flags.Africa","Africa"),polar:t("FlowerPainter.UI.Flags.Polar","Polar regions"),organizations:t("FlowerPainter.UI.Flags.Organizations","Organizations"),themes:t("FlowerPainter.UI.Flags.Themes","Medical and themes")};
    const name=(f:typeof current)=>t("FlowerPainter.Floral.flagTemplate."+f.id,f.en);
    const search=query.trim().toLowerCase();
    const shown=flags.filter(f=>(group==="all"||f.group===group)&&(!search||[f.id,f.code,f.en,f.zh,name(f)].join(" ").toLowerCase().includes(search)));
    const width=current.columns*Number(step.values.bloomSpacing||.28);
    const actualWidth=summary.stats.find(v=>v.key==="FlowerPainter.Floral.Stat.FlagWidth")?.value;
    const actualHeight=summary.stats.find(v=>v.key==="FlowerPainter.Floral.Stat.FlagHeight")?.value;
    // Gameface does not consistently honour object-fit; give the image its fitted dimensions.
    const imageSize=(ratio:number,w:number,h:number)=>({width:Math.min(w,h*ratio)+"rem",height:Math.min(h,w/ratio)+"rem"});
    return <div>
        <div className={s.flagCurrent} data-testid="flag-current"><img src={current.image} alt="" style={imageSize(current.ratio,80,54)}/><div><strong>{name(current)}</strong><div className={s.hint}>{t("FlowerPainter.UI.Flags.Ratio","Width : height {0} : 1",[current.ratio.toFixed(2)])}</div></div></div>
        <label className={s.hint} htmlFor="flag-search">{t("FlowerPainter.UI.Flags.Search","Search flags by name or code")}</label>
        <input id="flag-search" className={s.flagSearch} data-testid="flag-search" aria-label={t("FlowerPainter.UI.Flags.Search","Search flags by name or code")} value={query} onChange={e=>setQuery(e.target.value)} />
        <div className={s.buttons}>{Object.entries(groups).map(([id,label])=><Btn key={id} label={label} selected={id===group} testId={`flag-group-${id}`} onClick={()=>setGroup(id)}/>)}</div>
        <div className={s.hint}>{t("FlowerPainter.UI.Flags.Count","{0} / {1} templates",[shown.length,flags.length])}</div>
        <div className={s.flagList} data-testid="flag-list">{shown.map(f=><button type="button" className={s.flagTile} key={f.id} aria-pressed={current.id===f.id} data-testid={`flag-${f.id}`} title={name(f)} onClick={()=>act.setValue(step.path,"flagTemplate",f.id)}><div className={s.flagThumb}><img src={f.image} alt="" style={imageSize(f.ratio,65,40)}/></div><span>{name(f)}</span></button>)}</div>
        {!shown.length?<div className={s.hint}>{t("FlowerPainter.UI.Flags.NoMatches","No matching flags")}</div>:null}
        <div className={s.detail} data-testid="flag-size-advice">
            {!summary.stale&&actualWidth!==undefined&&actualHeight!==undefined?<div>{t("FlowerPainter.UI.Flags.ActualSize","Current flag: {0} × {1} m",[actualWidth.toFixed(1),actualHeight.toFixed(1)])}</div>:null}
            <div>{t("FlowerPainter.UI.Flags.RecommendedSize","Suggested size at this flower spacing: {0} × {1} m or larger",[width.toFixed(1),(width/current.ratio).toFixed(1)])}</div>
            <div className={s.hint}>{t("FlowerPainter.UI.Flags.SizeTip","Smaller flags can still be planted, but stars, lettering and emblems may lose detail. This is a visual guide, not a hard limit.")}</div>
            {!summary.stale?summary.issues.filter(i=>i.text.key.startsWith("FlowerPainter.Floral.Issue.Flag")).map(i=><div className={s.flagWarning} key={i.text.key}>{tl(i.text)}</div>):null}
        </div>
        <div className={s.hint}>{step.values.flagDirect ? t("FlowerPainter.UI.Flags.DirectTip","The template keeps its proportions. Set the flag width below.") : t("FlowerPainter.UI.Flags.Tip","Each flag keeps its original proportions. Irregular selections can cut the design; use a rectangular selection for a complete flag.")}</div>
    </div>;
};
