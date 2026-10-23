using System.Collections;
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
        private IEnumerator StepFlowerRegion(ScenarioState st,JObject step,JObject rec)
        {
            yield return EnsureDesign(st,rec,"Flower regions");
            string style=(string)step["style"]??"single";
            yield return UiAct(st,rec,"fill style","fill-style-"+style,null,null,()=>((string)_tool.SuiteValue("0","activeFillStyle"))==style,null,mustChange:false);
            if(style=="single") {
                string asset=(string)step["flower"]??Services.Assets.Items("flower").FirstOrDefault()?.Id;
                yield return UiAct(st,rec,"choose region flower","fill-flower",null,null,null,null,mustChange:false);
                JObject picked=null; yield return UiRun(SuiteUiSystem.ElementJs("asset-"+asset,click:true),r=>picked=r);
                yield return Frames(4);
                if((bool?)picked?["value"]?["ok"]!=true || ((string)_tool.SuiteValue("0","activeFillFlower"))!=asset){rec["error"]="region flower did not commit";yield break;}
                rec["flower"]=asset;
            }
            yield return UiAct(st,rec,"pick connected gap","fill-pick-region",null,null,()=>_tool.FloralIndex==-2,null,mustChange:false);
            var offset=step["at"] as JArray; var p=_tool.FloralOrigin+new float2((float?)offset?[0]??0,(float?)offset?[1]??0);
            var pos=_tool.FloralGround(p); yield return CameraTo(pos+new float3(35,0,0),0,80,250,new JObject(),null);
            _panelRectFrame=-100; yield return ReadPanelRect();
            string why=Clickable(pos,out var screen); if(why!=null){rec["error"]="fill target "+why;yield break;}
            RaycastResult? hit=null;yield return Raycast(screen,r=>hit=r);if(!hit.HasValue){rec["error"]="fill target has no raycast";yield break;}
            var h=hit.Value;
            FlowerPainterToolSystem.SuiteQueue(new FlowerPainterToolSystem.SuiteFrame{At=h.m_Hit.m_HitPosition,Entity=h.m_Owner,HasRaw=true,Raw=h.m_Hit,LeftDown=true,LeftHeld=true});
            FlowerPainterToolSystem.SuiteQueue(new FlowerPainterToolSystem.SuiteFrame{At=h.m_Hit.m_HitPosition,Entity=h.m_Owner,HasRaw=true,Raw=h.m_Hit});
            yield return Feed(rec);yield return WaitPlan(st,rec);
            var fills=FloralFill.Read(_tool.SuiteValue("0","fills") as string[]);
            rec["regionFills"]=fills.Count;rec["input"]="actual region-fill buttons and terrain raycast";
            if(!fills.Any(f=>f.Style==style)) rec["error"]="clicked gap did not receive its chosen fill";
            yield return UiAct(st,rec,"finish region fill",null,"selectFloralPattern",new object[]{"0",-1},()=>!_tool.FloralEditing,()=>_tool.SelectFloralPattern("0",-1),mustChange:false);
        }
    }
}
