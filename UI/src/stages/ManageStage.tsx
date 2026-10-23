// Manage — a built place under the cursor/selected: what it is, what it costs to run, and
// Edit / Rename / Remove (with confirmation). The same actions appear in the vanilla selected-info
// panel via FlowerPainterInfoSection.
import React, { useState } from "react";
import { ManagedGroup } from "../contract";
import { act } from "../lib/bindings";
import { Money } from "../lib/format";
import { useT } from "../lib/l10n";
import { Btn, Icon } from "../components/common";
import { Confirm } from "../components/dialog";
import { StatChip } from "./ReviewStage";
import s from "./stages.module.scss";

export function useGroupActions(id: number, displayName: string) {
    const { t } = useT();
    const [confirming, setConfirming] = useState<"remove" | "dissolve" | null>(null);
    const dissolve = confirming === "dissolve";
    const dialog = confirming ? (
        <Confirm
            title={dissolve ? t("FlowerPainter.UI.Manage.DissolveTitle", "Dissolve {0}?", [displayName]) : t("FlowerPainter.UI.Manage.RemoveTitle", "Remove {0}?", [displayName])}
            message={dissolve ? t("FlowerPainter.UI.Manage.DissolveText", "Keep all roads, plants and facilities. Release this area for new selections. Its pieces can be edited individually, but the group cannot be restored.") : t("FlowerPainter.UI.Manage.RemoveText", "Everything FlowerPainter built here (paths, surfaces, objects, roads it added) is bulldozed. Buildings that grew next to it stay.")}
            confirm={dissolve ? t("FlowerPainter.UI.Manage.Dissolve", "Dissolve area") : t("FlowerPainter.UI.Manage.RemoveConfirm", "Remove")}
            danger={!dissolve}
            onConfirm={() => {
                setConfirming(null);
                if (dissolve) act.dissolveGroup(id); else act.removeGroup(id);
            }}
            onCancel={() => setConfirming(null)}
        />
    ) : null;
    return { askRemove: () => setConfirming("remove"), askDissolve: () => setConfirming("dissolve"), dialog };
}

export const RenameField = ({ id, name, onDone }: { id: number; name: string; onDone: () => void }) => {
    const { t } = useT();
    const [value, setValue] = useState(name);
    const save = () => {
        act.renameGroup(id, value.trim());
        onDone();
    };
    return (
        <div className={s.saveRow} data-testid="rename-form">
            <input
                className={s.textInput}
                data-testid="rename-input"
                autoFocus
                value={value}
                onChange={(e) => setValue(e.target.value)}
                onKeyDown={(e) => {
                    e.stopPropagation();
                    if (e.key === "Enter") save();
                    if (e.key === "Escape") onDone();
                }}
            />
            <Btn variant="accent" label={t("FlowerPainter.UI.Save", "Save")} onClick={save} testId="rename-save" />
            <Btn icon="close" title={t("FlowerPainter.UI.Cancel", "Cancel")} onClick={onDone} />
        </div>
    );
};

export const ManageStage = ({ group }: { group: ManagedGroup | null }) => {
    const { t, tl } = useT();
    const [renaming, setRenaming] = useState(false);
    const name = group ? group.customName || tl(group.recipeName, t("FlowerPainter.Name", "FlowerPainter")) : "";
    const { askRemove, askDissolve, dialog } = useGroupActions(group?.id ?? -1, name);
    if (!group) {
        return (
            <div className={s.stage} data-testid="stage-manage">
                <div className={s.emptyNote}>{t("FlowerPainter.UI.Manage.Hover", "Click something you built to edit it")}</div>
            </div>
        );
    }
    return (
        <div className={s.stage} data-testid="stage-manage">
            <div className={s.manageHead}>
                <Icon name="logo" size={15} tint="accent" />
                {renaming ? (
                    <RenameField id={group.id} name={group.customName ?? ""} onDone={() => setRenaming(false)} />
                ) : (
                    <>
                        <div className={s.manageName} data-testid="manage-name">
                            {name}
                        </div>
                        {group.customName ? <div className={s.manageKind}>{tl(group.recipeName)}</div> : null}
                    </>
                )}
            </div>
            {group.stats.length ? (
                <div className={s.stats}>
                    {group.stats.map((st) => (
                        <StatChip key={st.key + st.unit} k={st.key} value={st.value} unit={st.unit} />
                    ))}
                </div>
            ) : null}
            <div className={s.manageUpkeep}>
                <div className={s.bigLabel}>{t("FlowerPainter.UI.Review.Upkeep", "Upkeep")}</div>
                <div className={s.manageUpkeepValue}>
                    <Money value={group.upkeep} perMonth />
                </div>
            </div>
            <Btn variant="solid" label={t("FlowerPainter.UI.Manage.Dissolve", "Dissolve area")} description={t("FlowerPainter.UI.Manage.DissolveTip", "Keep the built elements and release the area for new selections.")} onClick={askDissolve} testId="manage-dissolve" />
            <div className={s.manageActions}>
                <Btn
                    variant="accent"
                    icon="edit"
                    iconSize={12}
                    label={t("FlowerPainter.UI.Manage.Edit", "Edit")}
                    disabled={!group.canEdit}
                    description={!group.canEdit ? t("FlowerPainter.UI.Manage.CantEdit", "This was built by an older version and can only be removed.") : undefined}
                    onClick={() => act.editGroup(group.id)}
                    testId="manage-edit"
                />
                <Btn variant="solid" icon="edit" iconSize={12} label={t("FlowerPainter.UI.Manage.Rename", "Rename")} onClick={() => setRenaming(true)} testId="manage-rename" />
                <div className={s.flex} />
                <Btn variant="danger" icon="trash" iconSize={12} label={t("FlowerPainter.UI.Manage.Remove", "Remove")} onClick={askRemove} testId="manage-remove" />
            </div>
            {dialog}
        </div>
    );
};
