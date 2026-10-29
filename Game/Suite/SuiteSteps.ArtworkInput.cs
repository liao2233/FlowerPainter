using System.Collections;
using System.Linq;
using FlowerPainter.Core.Layers.Types.Floral;
using FlowerPainter.Core.Tool;
using Newtonsoft.Json.Linq;
using Unity.Mathematics;

namespace FlowerPainter.Game.Suite
{
    public sealed partial class SuiteRunner
    {
        private IEnumerator StepArtworkInput(ScenarioState st, JObject step, JObject rec)
        {
            yield return OpenTool();
            _tool.ChoosePlanting("flow");_tool.ClearArea();_tool.ChoosePlanting("");
            yield return Frames(15);
            yield return UiAct(st,rec,"choose flags","flower-mode-flag",null,null,()=>_tool.FlowerArtwork,null);
            var center=st.S.Block.Center;
            yield return CameraTo(P3(center),0,85,140,new JObject(),null);
            foreach(var offset in new[]{new float2(-15,-10),new float2(18,-8),new float2(9,17)})
                yield return Press(st,rec,P3(center+offset),false,h=>{});
            if(_tool.Draw.Count!=0 || _tool.Shape!=null) {rec["error"]="Flag design accepted free polygon points before placement";yield break;}
            if(_tool.GoToStage(ToolStage.Area)) {rec["error"]="Flag design allowed the area stage";yield break;}
            rec["freePointsBlocked"]=true;

            yield return UiAct(st,rec,"place flag","place-flag",null,null,()=>_tool.PlacingArtwork,null);
            yield return Press(st,rec,P3(center),true,h=>{});
            yield return Press(st,rec,P3(center+new float2(10,6)),false,h=>{});
            if(_tool.PlacingArtwork || _tool.Draw.Count!=0) {rec["error"]="Cancelled placement fell back to free drawing";yield break;}
            rec["cancelKeepsDrawingDisabled"]=true;

            yield return UiAct(st,rec,"place flag again","place-flag",null,null,()=>_tool.PlacingArtwork,null);
            yield return Press(st,rec,P3(center),false,h=>{});
            var ring=_tool.Draw.Points.ToArray();
            if(ring.Length!=4 || !_tool.DirectFlagEditing) {rec["error"]="Direct flag placement did not produce a rectangle";yield break;}
            float ratio=math.distance(ring[0].xz,ring[1].xz)/math.distance(ring[1].xz,ring[2].xz);
            if(math.abs(ratio-FlowerImage.For(_tool.CurrentRecipe.Root.Settings).Ratio)>.001f) {rec["error"]="Placed flag aspect ratio differs from its template";yield break;}
            yield return Press(st,rec,ring[0],true,h=>{});
            yield return Press(st,rec,P3(center+new float2(35,20)),false,h=>{});
            if(!ring.SequenceEqual(_tool.Draw.Points)) {rec["error"]="Map clicks edited individual flag corners";yield break;}
            rec["fixedAspect"]=ratio;rec["cornerEditingBlocked"]=true;

            _tool.ChoosePlanting("image");_tool.ClearArea();
            yield return Press(st,rec,P3(center),false,h=>{});
            if(_tool.Draw.Count!=0) {rec["error"]="Image mode accepted free polygon points";yield break;}
            rec["imageFreePointsBlocked"]=true;
            _tool.ChoosePlanting("flag");
        }
    }
}
