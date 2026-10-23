using System.Collections;
using Newtonsoft.Json.Linq;
using FlowerPainter.Core.Geometry;
using FlowerPainter.Core.Tool;
using FlowerPainter.Game.Tool;
using Unity.Mathematics;

namespace FlowerPainter.Game.Suite
{
    public sealed partial class SuiteRunner
    {
        private IEnumerator StepRectangle(ScenarioState st, JObject step, JObject rec)
        {
            yield return CameraTo(FocusOf(st.S.Block), 25f, 55f, 340f, new JObject(), null);
            yield return UiGoToStage(rec, ToolStage.Area);
            _tool.ClearArea();
            yield return UiSetAreaMode(rec, SelectionMode.Draw);
            yield return UiAct(st, rec, "strict rectangle", "points-rectangle", "setBrushRules", new object[] { "{\"rectangle\":true}" },
                () => _tool.Draw.Rectangle, () => { var r=_tool.BrushRules;r.Rectangle=true;_tool.SetBrushRules(r); });
            float angle=math.radians((float?)step["angle"] ?? 23f);
            var x=new float2(math.cos(angle), math.sin(angle));var y=new float2(-x.y,x.x);
            var centre=Pt(new JArray(0,0),st.S.Block,false);
            foreach (var offset in new[] { new float2(-55,-30), new float2(55,-30), new float2(73,34), new float2(-68,48) })
            {
                FlowerPainterToolSystem.SuiteClick(P3(centre + x*offset.x + y*offset.y));
                yield return Feed(rec,10f);
            }
            yield return Frames(5);
            if (!_tool.Draw.Closed || !RectangleSelection.IsRectangle(_tool.Shape?.Ring2D()))
            { rec["error"]="four clicks did not create a strict rectangle"; yield break; }
            var anchor=_tool.Draw.Points[2].xz;
            var from=_tool.Draw.Points[0];var to=from;to.xz-=x*12+y*8;
            FlowerPainterToolSystem.SuiteDrag(new[] { from,to },12);
            yield return Feed(rec,10f);yield return Frames(5);
            if (!RectangleSelection.IsRectangle(_tool.Shape?.Ring2D()) || math.distance(anchor,_tool.Draw.Points[2].xz)>.01f
                || math.distance(from.xz,_tool.Draw.Points[0].xz)<1f)
            { rec["error"]="corner drag broke the rectangle or moved the opposite corner"; yield break; }
            yield return KeyPress("undo");yield return Frames(3);
            if (!_tool.Draw.Rectangle || !_tool.Draw.Closed || math.distance(from.xz,_tool.Draw.Points[0].xz)>.01f)
            { rec["error"]="corner undo did not restore rectangle";yield break; }
            yield return KeyPress("redo");yield return Frames(3);
            from=(_tool.Draw.Points[1]+_tool.Draw.Points[2])*.5f;to=from;to.xz+=x*9;
            FlowerPainterToolSystem.SuiteDrag(new[] { from,to },12);
            yield return Feed(rec,10f);yield return Frames(5);
            if (!RectangleSelection.IsRectangle(_tool.Shape?.Ring2D())
                || math.distance(from.xz,(_tool.Draw.Points[1].xz+_tool.Draw.Points[2].xz)*.5f)<1f)
            { rec["error"]="edge drag broke the rectangle";yield break; }
            st.Ring=_tool.Shape.Ring2D();
            rec["corners"]=st.Ring.Length;
            var angles=new JArray();
            for(int i=0;i<4;i++)
            {
                var a=math.normalize(st.Ring[(i+3)%4]-st.Ring[i]);var b=math.normalize(st.Ring[(i+1)%4]-st.Ring[i]);
                angles.Add(math.degrees(math.acos(math.clamp(math.dot(a,b),-1f,1f))));
            }
            rec["angles"]=angles;
            rec["cornerAndEdgeDrag"]=true;rec["undoRedo"]=true;
            yield return WaitPlan(st,rec);
            Mod.Log.Info($"FlowerPainter rectangle audit: four corners, angles {angles}, drag and undo/redo kept right angles.");
        }
    }
}
