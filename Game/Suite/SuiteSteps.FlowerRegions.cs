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
            bool partition = _tool.FloralPartitioning;
            string style=(string)step["style"]??"single";
            var selectedFlowers = (step["flowers"] as JArray)?.Values<string>().ToArray()
                ?? new[] { (string)step["flower"]??Services.Assets.Items("flower").FirstOrDefault()?.Id };
            IEnumerator ChooseFill() {
                yield return UiAct(st,rec,"fill style","fill-style-"+style,null,null,null,null,mustChange:false);
                if(style=="single" || (partition && style!="empty")) {
                    yield return UiAct(st,rec,"choose region flower","fill-flower",null,null,null,null,mustChange:false);
                    foreach (string asset in selectedFlowers) {
                        JObject picked=null; yield return UiRun(SuiteUiSystem.ElementJs("asset-"+asset,click:true),r=>picked=r);
                        if((bool?)picked?["value"]?["found"]!=true){rec["error"]="region flower not found";rec["picker"]=picked;yield break;}
                        if(style=="single") break;
                    }
                    if (style != "single") yield return UiAct(st,rec,"finish prop selection","asset-picker-close",null,null,null,null,mustChange:false);
                    rec["flowers"] = new JArray(selectedFlowers);
                }
            }
            if (!partition) yield return ChooseFill();
            yield return UiAct(st,rec,"select regions",partition ? "partition-finish-boundaries" : "fill-pick-region",null,null,()=>_tool.FloralIndex==-2,null);
            var before = (_tool.SuiteValue("0", "fills") as string[]) ?? new string[0];
            var offset=step["at"] as JArray; var p=_tool.FloralOrigin+new float2((float?)offset?[0]??0,(float?)offset?[1]??0);
            var pos=_tool.FloralGround(p); yield return CameraTo(pos+new float3(35,0,0),0,80,250,new JObject(),null);
            _panelRectFrame=-100; yield return ReadPanelRect();
            string why=Clickable(pos,out var screen); if(why!=null){rec["error"]="fill target "+why;yield break;}
            RaycastResult? hit=null;yield return Raycast(screen,r=>hit=r);if(!hit.HasValue){rec["error"]="fill target has no raycast";yield break;}
            var h=hit.Value;
            FlowerPainterToolSystem.SuiteQueue(new FlowerPainterToolSystem.SuiteFrame{At=h.m_Hit.m_HitPosition,Entity=h.m_Owner,HasRaw=true,Raw=h.m_Hit,LeftDown=true,LeftHeld=true});
            FlowerPainterToolSystem.SuiteQueue(new FlowerPainterToolSystem.SuiteFrame{At=h.m_Hit.m_HitPosition,Entity=h.m_Owner,HasRaw=true,Raw=h.m_Hit});
            yield return Feed(rec);
            if (partition) {
                yield return Until(()=>_tool.SelectedFloralRegion>0,3f,"selected subdivision");
                rec["selectedRegion"] = _tool.SelectedFloralRegion;
                if(_tool.SelectedFloralRegion<1){rec["error"]="map click did not select a region";yield break;}
                var afterSelect = (_tool.SuiteValue("0", "fills") as string[]) ?? new string[0];
                if(!before.SequenceEqual(afterSelect)){rec["error"]="selection planted flowers before Apply";yield break;}
                var selected = _tool.FloralPartitionState().Regions.Find(r=>r.Id==_tool.SelectedFloralRegion);
                var others = before.Where((f,i)=>i!=selected.FillIndex).ToArray();
                yield return ChooseFill();
                yield return UiAct(st,rec,"apply selected region","partition-apply",null,null,null,null,mustChange:false);
                yield return Frames(4);
                var after = (_tool.SuiteValue("0", "fills") as string[]) ?? new string[0];
                if(others.Any(f=>!after.Contains(f))) rec["error"]="applying a region changed another region";
                var current = _tool.FloralPartitionState().Regions.Find(r=>r.Id==_tool.SelectedFloralRegion);
                var parsed = FloralFill.Read(after);
                if(current.FillIndex<0 || parsed[current.FillIndex].Style!=style || (style!="empty" && !parsed[current.FillIndex].Flowers.SequenceEqual(style=="single" ? selectedFlowers.Take(1) : selectedFlowers)))
                    rec["error"]="selected region did not retain its own props and style";
            }
            yield return WaitPlan(st,rec);
            var fills=FloralFill.Read(_tool.SuiteValue("0","fills") as string[]);
            rec["regionFills"]=fills.Count;rec["input"]="actual region selection, prop picker, style and Apply with terrain raycast";
            if(!fills.Any(f=>f.Style==style)) rec["error"]="clicked region did not receive its chosen fill";
            if (!partition) yield return UiAct(st,rec,"finish region fill",null,"selectFloralPattern",new object[]{"0",-1},()=>!_tool.FloralEditing,()=>_tool.SelectFloralPattern("0",-1),mustChange:false);
        }
    }
}
