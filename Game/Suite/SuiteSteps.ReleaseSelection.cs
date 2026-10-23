using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Common;
using Newtonsoft.Json.Linq;
using FlowerPainter.Core.Geometry;
using FlowerPainter.Core.Tool;
using FlowerPainter.Game.Groups;
using FlowerPainter.Game.Selection;
using FlowerPainter.Game.Tool;
using Unity.Entities;
using Unity.Mathematics;

namespace FlowerPainter.Game.Suite
{
    public sealed partial class SuiteRunner
    {
        private IEnumerator StepReleaseSelection(ScenarioState st, JObject step, JObject rec)
        {
            var registry = _world.GetExistingSystemManaged<GroupRegistrySystem>();
            var em = _world.EntityManager;
            var members = new List<Entity>();
            registry.GetMembers(st.Group, members);
            if (members.Count == 0) { rec["error"] = "no group to release"; yield break; }
            var objects = new Dictionary<Entity, global::Game.Objects.Transform>();
            var curves = new Dictionary<Entity, global::Game.Net.Curve>();
            var visible = new HashSet<Entity>();
            foreach (var e in members)
            {
                if (em.HasComponent<global::Game.Objects.Transform>(e)) objects[e] = em.GetComponentData<global::Game.Objects.Transform>(e);
                if (em.HasComponent<global::Game.Net.Curve>(e)) curves[e] = em.GetComponentData<global::Game.Net.Curve>(e);
                if (!em.HasComponent<Overridden>(e)) visible.Add(e);
            }
            var originalRing = st.Ring;
            Poly.Bounds(originalRing, out var min, out var max);
            var obstacles = new BlockObstacles(em, _world);
            int before = obstacles.Gather(min, max, 0, true).Count(o => o.Kind == BrushObstacleKind.FlowerPainterBuild);
            rec["groupStopsBefore"] = before;
            if (before == 0) { rec["error"] = "baseline missing built-area stop"; yield break; }
            int builds = _tool.SuiteBuildsDone;
            yield return UiGoToStage(rec, ToolStage.Area);
            yield return UiAct(st, rec, "force selection on", "force-selection", "setBrushRules", new object[] { "{\"forceSelection\":true}" },
                () => _tool.BrushRules.ForceSelection, () => { var r = _tool.BrushRules; r.ForceSelection = true; _tool.SetBrushRules(r); });
            var modes = new JArray(); rec["forceSelections"] = modes;
            foreach (string mode in new[] { "points", "bucket", "paint", "paintanything" })
            {
                var areaStep = JObject.Parse("{\"rect\":[40,40],\"size\":36,\"at\":[0,0],\"stroke\":[[-10,0],[10,0]]}");
                areaStep["tool"] = mode;
                var result = new JObject { ["mode"] = mode }; modes.Add(result);
                yield return StepArea(st, areaStep, result);
                yield return Frames(5);
                var ring = _tool.Shape?.Ring2D();
                bool covers = ring != null && Poly.Contains(ring, (min + max) * .5f);
                result["coversOriginalCentre"] = covers;
                if (!covers || result["error"] != null || _tool.SuiteBuildsDone != builds)
                { rec["error"] = "force selection failed or edited old group in " + mode; yield break; }
            }
            st.Ring = originalRing;
            _tool.ClearArea();
            yield return UiAct(st, rec, "force selection off", "force-selection", "setBrushRules", new object[] { "{\"forceSelection\":false}" },
                () => !_tool.BrushRules.ForceSelection, () => { var r = _tool.BrushRules; r.ForceSelection = false; _tool.SetBrushRules(r); });
            yield return SelectBuild(st, rec);
            if (rec["error"] != null) yield break;
            yield return UiAct(st, rec, "Dissolve", "manage-dissolve", null, null, null, null, mustChange: false, effectFrames: 4);
            yield return UiAct(st, rec, "OK (dissolve)", "confirm-ok", "dissolveGroup", new object[] { (double)st.Group },
                () => registry.IsDissolved(st.Group), () => _tool.DissolveGroup(st.Group), mustChange: false);
            yield return Frames(30);
            int changed = 0;
            foreach (var e in members)
            {
                if (!em.Exists(e) || em.HasComponent<Deleted>(e)) { changed++; continue; }
                if (objects.TryGetValue(e, out var t) && !em.GetComponentData<global::Game.Objects.Transform>(e).Equals(t)) changed++;
                if (curves.TryGetValue(e, out var c) && !em.GetComponentData<global::Game.Net.Curve>(e).Equals(c)) changed++;
                if (visible.Contains(e) && em.HasComponent<Overridden>(e)) changed++;
                if (registry.GroupOf(e) != 0 || registry.TryLiftToOwner(e, out _) || registry.IsOwnerlessGroupPart(e, out _)) changed++;
            }
            int after = obstacles.Gather(min, max, 0, true).Count(o => o.Kind == BrushObstacleKind.FlowerPainterBuild);
            rec["membersPreserved"] = members.Count;
            rec["unexpectedChanges"] = changed;
            rec["groupStopsAfter"] = after;
            bool hidden = registry.Get(st.Group) == null && registry.GetReceipt(st.Group) == null && !registry.AllGroups.Contains(st.Group);
            rec["ownershipReleased"] = hidden;
            if (changed != 0 || after != 0 || !hidden) rec["error"] = "dissolution changed existing content or retained public ownership/stops";
            Mod.Log.Info($"FlowerPainter release audit: group {st.Group}, members={members.Count}, changed={changed}, stops={before}->{after}, released={hidden}");
        }
    }
}
