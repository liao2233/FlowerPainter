using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Common;
using Newtonsoft.Json.Linq;
using FlowerPainter.Core.Layers.Types.Floral;
using FlowerPainter.Game.Tool;
using Unity.Mathematics;

namespace FlowerPainter.Game.Suite
{
    public sealed partial class SuiteRunner
    {
        // Exercise the actual panel and the tool's drag gesture, with the game's terrain raycasts.
        private IEnumerator StepFloralMotif(ScenarioState st, JObject step, JObject rec)
        {
            yield return EnsureDesign(st, rec, "Options");
            if (rec["error"] != null) yield break;
            string action = (string)step["action"] ?? "select";
            var before = _tool.FloralPatterns();
            int index = (int?)step["index"] ?? 0;
            if (action == "add")
            {
                string kind = (string)step["kind"] ?? "ring";
                yield return UiAct(st, rec, "add " + kind, "motif-add-" + kind, null, null,
                    () => _tool.FloralPatterns().Count == before.Count + 1, null);
                yield return WaitPlan(st, rec); yield break;
            }
            if (action == "fill" || action == "frame")
            {
                bool fillBefore = (bool?)_tool.SuiteValue("0", "fillGaps") ?? false;
                yield return UiAct(st, rec, action, action == "fill" ? "motif-fill-gaps" : "motif-edit-frame", null, null,
                    () => action == "fill" ? ((bool?)_tool.SuiteValue("0", "fillGaps") ?? false) != fillBefore : !_tool.FloralEditing,
                    null, mustChange: action == "fill");
                if (!before.Select(m => m.Encode()).SequenceEqual(_tool.FloralPatterns().Select(m => m.Encode()))) rec["error"] = "gap fill changed manual patterns";
                yield return WaitPlan(st, rec); yield break;
            }
            if (index < 0 || index >= before.Count) { rec["error"] = "unknown flower pattern " + index; yield break; }
            yield return UiAct(st, rec, "select pattern " + index, "motif-select-" + index, null, null,
                () => _tool.FloralEditing && _tool.FloralIndex == index, null);
            if (rec["error"] != null || action == "select") yield break;
            if (action == "flower")
            {
                string asset = (string)step["asset"];
                yield return UiAct(st, rec, "pattern flower", "motif-flower", null, null, null, null);
                JObject picked = null;
                yield return UiRun(SuiteUiSystem.ElementJs("asset-" + asset, click: true), r => picked = r);
                yield return Frames(4);
                if ((bool?)picked?["value"]?["found"] != true || _tool.FloralPatterns()[index].Flower != asset) rec["error"] = "pattern flower picker did not select " + asset;
                rec["flower"] = _tool.FloralPatterns()[index].Flower;
                yield return WaitPlan(st, rec); yield break;
            }
            int handle = action == "scale" ? 1 : action == "rotate" ? 2 : 0;
            var motif = before[index];
            var center = motif.Center(_tool.FloralOrigin);
            var start = _tool.FloralHandlePoint(motif, handle);
            var end = start;
            if (action == "move" && step["by"] is JArray by) end += new float2((float)by[0], (float)by[1]);
            else if (action == "scale") end = center + (start - center) * ((float?)step["factor"] ?? 1.2f);
            else if (action == "rotate")
            {
                math.sincos(math.radians((float?)step["degrees"] ?? 30f), out float s, out float c);
                var d = start - center; end = center + new float2(d.x * c - d.y * s, d.x * s + d.y * c);
            }
            else if (action != "move") { rec["error"] = "unknown motif action " + action; yield break; }
            var camera = new JObject();
            yield return CameraTo(_tool.FloralGround((start + end) * 0.5f) + new float3(35, 0, 0), 0, 80, 250, camera, null);
            _panelRectFrame = -100;
            yield return ReadPanelRect();
            var hits = new List<RaycastResult>();
            for (int i = 0; i <= 12; i++)
            {
                var point = _tool.FloralGround(math.lerp(start, end, i / 12f));
                string why = Clickable(point, out var screen);
                if (why != null) { rec["error"] = "pattern handle " + why; yield break; }
                RaycastResult? hit = null;
                yield return Raycast(screen, r => hit = r);
                if (!hit.HasValue) { rec["error"] = "no terrain raycast for pattern drag"; yield break; }
                hits.Add(hit.Value);
            }
            for (int i = 0; i < hits.Count; i++)
            {
                var h = hits[i];
                FlowerPainterToolSystem.SuiteQueue(new FlowerPainterToolSystem.SuiteFrame { At = h.m_Hit.m_HitPosition, Entity = h.m_Owner, HasRaw = true, Raw = h.m_Hit, LeftDown = i == 0, LeftHeld = true });
            }
            var last = hits.Last();
            FlowerPainterToolSystem.SuiteQueue(new FlowerPainterToolSystem.SuiteFrame { At = last.m_Hit.m_HitPosition, Entity = last.m_Owner, HasRaw = true, Raw = last.m_Hit });
            yield return Feed(rec);
            yield return WaitPlan(st, rec);
            var after = _tool.FloralPatterns();
            rec["input"] = "13 real screen raycasts through the tool mouse gesture";
            rec["before"] = motif.Encode(); rec["after"] = after[index].Encode();
            if (motif.Encode() == after[index].Encode()) rec["error"] = "drag changed nothing";
            for (int i = 0; i < before.Count; i++)
                if (i != index && before[i].Encode() != after[i].Encode()) rec["error"] = "drag changed another flower pattern";
            var expected = motif.Clone();
            var a = hits[0].m_Hit.m_HitPosition.xz; var b = last.m_Hit.m_HitPosition.xz;
            if (handle == 0) { expected.X += b.x - a.x; expected.Z += b.y - a.y; }
            else if (handle == 1) expected.Size *= math.distance(b, center) / math.distance(a, center);
            else expected.Angle += math.degrees(math.atan2(b.y - center.y, b.x - center.x) - math.atan2(a.y - center.y, a.x - center.x));
            var actual = after[index];
            if (math.abs(expected.X - actual.X) > 0.05f || math.abs(expected.Z - actual.Z) > 0.05f || math.abs(expected.Size - actual.Size) > 0.05f || math.abs(expected.Angle - actual.Angle) > 0.1f) rec["error"] = "pattern drag did not match the requested transform";
            if ((bool?)step["verifyUndo"] == true)
            {
                yield return KeyPress("undo");
                bool undo = before.Select(m => m.Encode()).SequenceEqual(_tool.FloralPatterns().Select(m => m.Encode()));
                yield return KeyPress("redo");
                bool redo = after.Select(m => m.Encode()).SequenceEqual(_tool.FloralPatterns().Select(m => m.Encode()));
                rec["undo"] = undo; rec["redo"] = redo;
                if (!undo || !redo) rec["error"] = "pattern drag was not a single undo/redo operation";
                yield return WaitPlan(st, rec);
            }
        }
    }
}
