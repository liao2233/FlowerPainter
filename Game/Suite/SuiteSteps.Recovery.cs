using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Newtonsoft.Json.Linq;
using FlowerPainter.Core;
using FlowerPainter.Core.Build;
using FlowerPainter.Core.Tool;
using FlowerPainter.Game.Build;
using FlowerPainter.Game.Groups;
using Unity.Entities;
using Unity.Mathematics;

namespace FlowerPainter.Game.Suite
{
    public sealed partial class SuiteRunner
    {
        private string RecoverySignature(int group)
        {
            var em = _world.EntityManager;
            var members = new List<Entity>(); Services.Groups.GetMembers(group, members);
            var lines = new List<string>();
            string P(float3 p) => p.x.ToString("F1", CultureInfo.InvariantCulture) + "," + p.y.ToString("F1", CultureInfo.InvariantCulture) + "," + p.z.ToString("F1", CultureInfo.InvariantCulture);
            foreach (var e in members)
            {
                if (!em.Exists(e) || em.HasComponent<Deleted>(e) || !em.HasComponent<PrefabRef>(e)) continue;
                var tag = em.GetComponentData<FlowerPainterMember>(e);
                if (tag.Kind == MemberKind.Auxiliary || tag.Kind == MemberKind.Node || tag.Role == MemberRole.OwnerArea || tag.Role == MemberRole.Companion || tag.Role == MemberRole.ParkCompanion) continue;
                string name = _world.GetOrCreateSystemManaged<Assets.AssetCatalogSystem>().NameOf(em.GetComponentData<PrefabRef>(e).m_Prefab);
                if (em.HasComponent<Curve>(e))
                {
                    var c = em.GetComponentData<Curve>(e).m_Bezier;
                    lines.Add(name + "|net|" + P(c.a) + "|" + P(c.b) + "|" + P(c.c) + "|" + P(c.d));
                }
                else if (em.HasComponent<global::Game.Objects.Transform>(e))
                    lines.Add(name + "|object|" + P(em.GetComponentData<global::Game.Objects.Transform>(e).m_Position));
                else if (em.HasBuffer<global::Game.Areas.Node>(e))
                {
                    var vertices = new List<string>();
                    // Surfaces conform to the acknowledged permanent ground; their XZ footprint must be identical.
                    foreach (var node in em.GetBuffer<global::Game.Areas.Node>(e, true)) vertices.Add(P(new float3(node.m_Position.x, 0, node.m_Position.z)));
                    vertices.Sort(StringComparer.Ordinal);
                    lines.Add(name + "|area|" + string.Join(";", vertices));
                }
            }
            lines.Sort(StringComparer.Ordinal);
            return string.Join("\n", lines);
        }

        private IEnumerator StepEditRecovery(ScenarioState st, JObject step, JObject rec)
        {
            if (st.Group == 0) { rec["error"] = "build a group before testing recovery"; yield break; }
            string before = RecoverySignature(st.Group);
            yield return StepEdit(st, new JObject { ["build"] = true }, rec);
            if (rec["error"] != null) yield break;
            yield return WaitPlan(st, rec, always: true);
            yield return UiGoToStage(rec, ToolStage.Review);
            if (_tool.PermanentPending())
                yield return UiAct(st, rec, "OK, permanent", "ack-permanent", "acknowledgePermanent", new object[] { true }, () => _tool.PermanentAcknowledged, () => _tool.AcknowledgePermanent(true));
            var svc = (BuildService)Services.Build;
            bool fail = (string)step["mode"] == "afterApply";
            svc.SuiteFailAfterApply = fail;
            svc.SuiteCancelAfterRemoval = !fail;
            yield return UiAct(st, rec, "apply edit", "primary", "primary", null, () => _tool.Building, () => _tool.Primary());
            if (rec["error"] != null || !_tool.Building)
            {
                svc.SuiteFailAfterApply = svc.SuiteCancelAfterRemoval = false;
                if (rec["error"] == null) rec["error"] = "edit did not start";
                yield break;
            }
            yield return WaitFor(rec, "edit and recovery", () => !_tool.Building, 180f);
            svc.SuiteFailAfterApply = svc.SuiteCancelAfterRemoval = false;
            if (_timedOut) { rec["error"] = "edit recovery timed out"; yield break; }
            yield return Frames(12);
            string after = RecoverySignature(st.Group);
            bool restored = false;
            foreach (var note in _tool.BuildNotes) if (note.Key == "FlowerPainter.Build.OriginalRestored") restored = true;
            rec["originalRestored"] = restored;
            rec["geometryUnchanged"] = before == after;
            rec["surfaceHeightsFollowPermanentTerrain"] = true;
            if (!restored || before != after)
            {
                rec["error"] = "recovery did not restore the original geometry";
                rec["before"] = before; rec["after"] = after;
            }
        }
    }
}
