using System;
using System.Collections;
using System.IO;
using FlowerPainter.Core.Layers.Types.Floral;
using Newtonsoft.Json.Linq;
using Unity.Mathematics;

namespace FlowerPainter.Game.Suite
{
    public sealed partial class SuiteRunner
    {
        private IEnumerator ClipboardFixture(string directory,string kind)
        {
            string id=Guid.NewGuid().ToString("N");
            File.WriteAllText(Path.Combine(directory,"request.json"),new JObject{{"id",id},{"kind",kind}}.ToString());
            string ack=Path.Combine(directory,id+".ready");
            yield return Until(()=>File.Exists(ack),20,"external clipboard fixture "+kind);
            if(!File.Exists(ack))throw new TimeoutException("Start the external Clipboard-Fixture.ps1 host before this scenario");
        }

        private IEnumerator StepPasteImage(ScenarioState st,JObject step,JObject rec)
        {
            yield return OpenTool();_tool.ChoosePlanting("image");_tool.ClearArea();yield return Frames(15);
            string directory=(string)step["fixtureDir"];
            if(string.IsNullOrEmpty(directory)||!Directory.Exists(directory))throw new InvalidOperationException("Clipboard fixture directory missing");
            yield return ClipboardFixture(directory,"png");
            yield return UiAct(st,rec,"paste PNG","paste-image",null,null,()=>_tool.StateInput().ImportingImage || _tool.CurrentRecipe.Root.Settings.S("imageName","")=="Clipboard.png",null);
            yield return Until(()=>!_tool.StateInput().ImportingImage,20,"clipboard PNG import");
            var original=_tool.CurrentRecipe.Root.Settings.S("imageData","");
            var map=FlowerImage.Read(original);
            if(math.abs(map.Ratio-1.5f)>.001f || map.Color(new float2(.25f,.75f))!=FlowerImage.Nearest(255,0,0,255) || map.Color(new float2(.75f,.1f))!=-1)
                throw new InvalidDataException("Clipboard PNG lost colour, alpha or aspect ratio: "+_tool.StateInput().ImageImportError);
            rec["pngAlphaAndAspect"]=true;

            yield return ClipboardFixture(directory,"text");
            yield return UiAct(st,rec,"paste text is rejected","paste-image",null,null,()=>_tool.StateInput().ImportingImage || !string.IsNullOrEmpty(_tool.StateInput().ImageImportError),null);
            yield return Until(()=>!_tool.StateInput().ImportingImage,20,"clipboard non-image rejection");
            if(_tool.CurrentRecipe.Root.Settings.S("imageData","")!=original || string.IsNullOrEmpty(_tool.StateInput().ImageImportError))
                throw new InvalidDataException("Non-image clipboard replaced the design or had no error");
            rec["nonImagePreservesDesign"]=true;

            yield return ClipboardFixture(directory,"bitmap");
            yield return UiAct(st,rec,"paste screenshot bitmap","paste-image",null,null,()=>_tool.StateInput().ImportingImage || string.IsNullOrEmpty(_tool.StateInput().ImageImportError),null);
            yield return Until(()=>!_tool.StateInput().ImportingImage,20,"clipboard bitmap import");
            map=FlowerImage.Read(_tool.CurrentRecipe.Root.Settings.S("imageData",""));
            if(map.Color(new float2(.5f,.5f))!=FlowerImage.Nearest(255,255,0,255))throw new InvalidDataException("Clipboard bitmap import failed: "+_tool.StateInput().ImageImportError);
            rec["bitmapImport"]=true;

            yield return ClipboardFixture(directory,"file");
            yield return UiAct(st,rec,"paste copied file","paste-image",null,null,()=>_tool.StateInput().ImportingImage || _tool.CurrentRecipe.Root.Settings.S("imageName","")=="clipboard-fixture.png",null);
            yield return Until(()=>!_tool.StateInput().ImportingImage,20,"clipboard file import");
            map=FlowerImage.Read(_tool.CurrentRecipe.Root.Settings.S("imageData",""));
            if(map.Color(new float2(.75f,.1f))!=-1)throw new InvalidDataException("Copied file lost transparency");
            rec["fileImport"]=true;
        }
    }
}