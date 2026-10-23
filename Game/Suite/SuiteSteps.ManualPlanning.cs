using System.Collections;
using Newtonsoft.Json.Linq;
using FlowerPainter.Core.Tool;
namespace FlowerPainter.Game.Suite
{
    public sealed partial class SuiteRunner
    {
        private IEnumerator StepManualPlanning(ScenarioState st, JObject step, JObject rec)
        {
            yield return UiGoToStage(rec,ToolStage.Design);
            int requests=_tool.PlanningRequests;
            yield return Frames(90);
            if (_tool.Planning || _tool.PlanningRequests!=requests || _tool.PlanRevision==_tool.InputRevision)
            {rec["error"]="planning ran automatically before the button"; yield break;}
            if ((bool?)step["missing"]==true)
            {
                if (_tool.Facts().UnreadyStepPath==null) {rec["error"]="test expected a required entrance";yield break;}
                _tool.StartPlanning();yield return Frames(10);
                if (_tool.PlanningRequests!=requests) rec["error"]="planning accepted missing entrance";
                rec["missingEntranceBlocked"]=true;
            }
            else
            {
                yield return UiAct(st,rec,"start planning","primary","startPlanning",null,
                    ()=>_tool.PlanningRequests>requests,()=>_tool.StartPlanning());
                yield return WaitPlan(st,rec,always:true);
                if (_tool.PlanningRequests!=requests+1 || !PlanReady()) rec["error"]="button did not submit exactly one current plan";
                rec["oneExplicitRequest"]=true;
            }
            rec["noAutomaticRequests"]=true;
        }
    }
}
