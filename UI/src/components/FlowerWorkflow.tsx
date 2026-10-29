import React from "react";
import {act} from "../lib/bindings";
import {useT} from "../lib/l10n";
import {Btn,Section} from "./common";
import s from "./floral.module.scss";
export const FlowerMethods=()=>{const {t}=useT();return <Section title={t("FlowerPainter.Workflow.Choose","What would you like to plant?")}><div className={s.buttons}>
{[["flow","carpet","Flowers.Carpet","Fill the area"],["partition","partition","Flowers.Partition","Subdivide the frame"],["collage","compose","Flowers.Compose","Curves and patterns"],["flag","flag","Flags.Title","Flags"],["image","image","Images.Title","Image flowers"]].map(([mode,id,key,label])=><Btn key={mode} label={t("FlowerPainter.UI."+key,label)} onClick={()=>act.choosePlanting(mode)} testId={"flower-mode-"+id}/>)}</div><div className={s.hint}>{t("FlowerPainter.Workflow.StartTip","Images and flags: design, place, then plant. Other flowers: draw an area first. Click existing flowers on the map to edit them.")}</div></Section>};
export const FlowerDone=()=>{const {t}=useT();return <Section title={t("FlowerPainter.Workflow.Planted","Planted")}><div className={s.hint}>{t("FlowerPainter.Workflow.DoneTip","Your flowers are built. Place another copy, or finish. Click a built group to edit it.")}</div><Btn variant="accent" label={t("FlowerPainter.Workflow.Repeat","Place another")} onClick={()=>act.repeatFlowers()} testId="flower-repeat"/><Btn label={t("FlowerPainter.Workflow.Finish","Finish")} onClick={()=>act.close()} testId="flower-finish"/></Section>};
