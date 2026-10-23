using System.Collections;
using System.Collections.Generic;
using Game.Common;
using Newtonsoft.Json.Linq;
using FlowerPainter.Core.Geometry;
using FlowerPainter.Core.Tool;
using FlowerPainter.Game.Groups;
using FlowerPainter.Game.Tool;
using Unity.Entities;
using Unity.Mathematics;

namespace FlowerPainter.Game.Suite
{
    public sealed partial class SuiteRunner
    {
        private IEnumerator StepBrushBuilt(ScenarioState st, JObject step, JObject rec)
        {
            yield return UiGoToStage(rec, ToolStage.Area);
            yield return UiSetAreaMode(rec, SelectionMode.BlockPaint);
            float size = (float?)step["size"] ?? 30f;
            bool erase = (bool?)step["erase"] ?? false, square = (bool?)step["square"] ?? false;
            _tool.SetBrush(square, size, 0);
            var path = new List<float3>();
            foreach (var p in (JArray)step["stroke"]) path.Add(P3(Pt(p, st.S.Block, (bool?)step["abs"] ?? false)));
            var mask = new BrushMask(2f);
            mask.Stamp(path[0].xz, size * 0.5f, false, square);
            for (int i=1;i<path.Count;i++) mask.Stroke(path[i-1].xz,path[i].xz,size*0.5f,false,square);
            var stable = new Dictionary<Entity, global::Game.Objects.Transform>();
            var visible = new HashSet<Entity>();
            var registry = _world.GetExistingSystemManaged<GroupRegistrySystem>();
            var em = _world.EntityManager;
            var members = new List<Entity>();
            registry.GetMembers(st.Group, members);
            foreach (var e in members)
                if (em.HasComponent<global::Game.Objects.Transform>(e))
                {
                    var t = em.GetComponentData<global::Game.Objects.Transform>(e);
                    if (!mask.IsPaintedAt(t.m_Position.xz))
                    {
                        stable[e]=t;
                        if (!em.HasComponent<Overridden>(e)) visible.Add(e);
                    }
                }
            int count = _tool.SuiteBuildsDone;
            yield return Frames(3);
            FlowerPainterToolSystem.SuiteDrag(path.ToArray(), 12, right:erase);
            yield return Feed(rec,60f);
            yield return WaitFor(rec,"brush-build",()=>!_tool.Building && !_tool.Painting && !_tool.Planning,180f);
            if (_tool.SuiteBuildsDone == count) { rec["error"]="brush did not complete a local build: " + _tool.FlashText?.Key; yield break; }
            yield return Frames(15);
            int changed = 0;
            foreach (var kv in stable)
            {
                if (!em.Exists(kv.Key) || em.HasComponent<Deleted>(kv.Key) || !em.HasComponent<global::Game.Objects.Transform>(kv.Key)) { changed++; Mod.Log.Warn($"Brush lost untouched {kv.Key} at {kv.Value.m_Position}"); continue; }
                var t=em.GetComponentData<global::Game.Objects.Transform>(kv.Key);
                if (!t.m_Position.Equals(kv.Value.m_Position) || !t.m_Rotation.Equals(kv.Value.m_Rotation)) { changed++; Mod.Log.Warn($"Brush moved untouched {kv.Key}: {kv.Value.m_Position} -> {t.m_Position}; rotation {kv.Value.m_Rotation} -> {t.m_Rotation}"); }
                if (visible.Contains(kv.Key) && em.HasComponent<Overridden>(kv.Key)) { changed++; Mod.Log.Warn($"Brush hid untouched {kv.Key} at {t.m_Position}"); }
            }
            rec["untouchedObjects"]=stable.Count;
            rec["unexpectedChanges"]=changed;
            var footprint=registry.GetRecord(st.Group)?.Footprint();
            rec["regions"]=footprint?.Count ?? 0;
            double area=0; int holes=0;
            if(footprint!=null) foreach(var r in footprint) { area+=r.Area; holes+=r.Holes.Count; }
            rec["area"]=area; rec["holes"]=holes;
            if (changed != 0) rec["error"]=$"{changed} untouched objects changed";
            if(step["expectRegions"]!=null && (int)step["expectRegions"]!=(footprint?.Count??0)) rec["error"]="unexpected footprint region count";
            if ((int?)step["expectRegions"] == 0)
            {
                Poly.Bounds(st.Ring, out var min, out var max);
                int leftovers=0;
                var query=em.CreateEntityQuery(ComponentType.ReadOnly<global::Game.Net.Edge>(),ComponentType.ReadOnly<global::Game.Net.Curve>(),ComponentType.Exclude<Deleted>(),ComponentType.Exclude<global::Game.Tools.Temp>());
                using(var edges=query.ToEntityArray(Unity.Collections.Allocator.Temp))
                    foreach(var e in edges)
                    {
                        var p=Colossal.Mathematics.MathUtils.Position(em.GetComponentData<global::Game.Net.Curve>(e).m_Bezier,.5f).xz;
                        if(math.all(p>=min-45f)&&math.all(p<=max+45f)) leftovers++;
                    }
                query.Dispose();
                rec["leftoverEdges"]=leftovers;
                if(leftovers!=0) rec["error"]=$"{leftovers} edges survived complete brush erase on open test land";
            }
            Mod.Log.Info($"FlowerPainter brush preservation: group {st.Group}, untouched={stable.Count}, changed={changed}, regions={footprint?.Count??0}, holes={holes}");
        }
    }
}
