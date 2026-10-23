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
            if(action=="draw" || action=="removeNode") {
                var points=step["points"] as JArray;
                if(action=="draw") yield return UiAct(st,rec,"draw free curve","motif-draw-curve",null,null,()=>_tool.FloralDrawing,null);
                int n=action=="draw"?(points?.Count??0):1;
                for(int i=0;i<n;i++) {
                    float2 p=action=="draw"?_tool.FloralOrigin+new float2((float)points[i][0],(float)points[i][1]):_tool.FloralHandlePoint(_tool.FloralPatterns()[index],10+((int?)step["point"]??0));
                    var pos=_tool.FloralGround(p);
                    yield return CameraTo(pos+new float3(35,0,0),0,80,250,new JObject(),null);
                    _panelRectFrame=-100;yield return ReadPanelRect();
                    string why=Clickable(pos,out var screen);if(why!=null){rec["error"]="curve point "+why;yield break;}
                    RaycastResult? hit=null;yield return Raycast(screen,r=>hit=r);if(!hit.HasValue){rec["error"]="curve point has no terrain raycast";yield break;}
                    FlowerPainterToolSystem.SuiteClickHit(hit.Value.m_Owner,hit.Value.m_Hit,action=="removeNode");yield return Feed(rec);
                }
                if(action=="draw") yield return KeyPress("enter");
                var actualPoints=_tool.FloralPatterns()[index].Points;
                rec["controlPoints"]=actualPoints.Length;
                if(actualPoints.Length!=(action=="draw"?n:before[index].Points.Length-1))rec["error"]="curve did not retain the requested control points";
                yield return WaitPlan(st,rec,always:true);yield break;
            }
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
            int handle = action == "scale" ? 1 : action == "rotate" ? 2 : action == "width" ? 3 : action == "node" ? 10+((int?)step["point"]??0) : 0;
            var motif = before[index];
            var center = motif.Center(_tool.FloralOrigin);
            var start = _tool.FloralHandlePoint(motif, handle);
            var end = start;
            if ((action == "move" || action == "node") && step["by"] is JArray by) end += new float2((float)by[0], (float)by[1]);
            else if (action == "scale" || action == "width") end = center + (start - center) * ((float?)step["factor"] ?? 1.2f);
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
            if (handle >= 10) { var d=b-center;math.sincos(math.radians(expected.Angle),out float sn,out float cs);expected.Points[handle-10]=new float2(d.x*cs+d.y*sn,-d.x*sn+d.y*cs)/expected.Size; }
            else if (handle == 3) expected.Width=math.clamp((math.distance(b,center)-3)*2,0.5f,100);
            else if (handle == 0) { expected.X += b.x - a.x; expected.Z += b.y - a.y; }
            else if (handle == 1) expected.Size *= math.distance(b, center) / math.distance(a, center);
            else expected.Angle += math.degrees(math.atan2(b.y - center.y, b.x - center.x) - math.atan2(a.y - center.y, a.x - center.x));
            var actual = after[index];
            if(math.abs(expected.Width-actual.Width)>0.05f || (handle>=10 && math.distance(expected.Points[handle-10],actual.Points[handle-10])>0.005f)) rec["error"]="ribbon width or curve node drag did not match the pointer";
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
