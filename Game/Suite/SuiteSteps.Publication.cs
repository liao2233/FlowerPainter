using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using FlowerPainter.Core.Tool;
using FlowerPainter.Game.Assets;
using FlowerPainter.Game.Tool;
using Newtonsoft.Json.Linq;
using Unity.Mathematics;

namespace FlowerPainter.Game.Suite
{
    public sealed partial class SuiteRunner
    {
        private IEnumerator StepPublication(ScenarioState st,JObject step,JObject rec)
        {
            yield return OpenTool();
            string view=(string)step["view"];
            if(view=="home") {_tool.ChoosePlanting("");yield return Frames(15);yield break;}
            if(view=="image") {
                // Original generated illustration used as a real importer input, not a mock game screenshot.
                string path=Path.Combine(_out,"sunflower.png");
                using(var bitmap=new Bitmap(384,256,PixelFormat.Format32bppArgb)) {
                    using(var g=Graphics.FromImage(bitmap)) {
                        g.Clear(Color.Transparent);g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        using(var stem=new Pen(Color.FromArgb(40,135,62),12))g.DrawLine(stem,192,110,192,238);
                        using(var leaf=new SolidBrush(Color.FromArgb(72,177,75))) {g.FillEllipse(leaf,136,170,58,25);g.FillEllipse(leaf,192,195,58,25);}
                        g.TranslateTransform(192,96);
                        using(var petal=new SolidBrush(Color.FromArgb(255,197,35)))for(int i=0;i<12;i++){g.FillEllipse(petal,-18,-79,36,68);g.RotateTransform(30);}
                        using(var center=new SolidBrush(Color.FromArgb(130,58,32)))g.FillEllipse(center,-30,-30,60,60);
                        using(var dots=new SolidBrush(Color.FromArgb(227,137,37)))for(int y=-17;y<=17;y+=8)for(int x=-17;x<=17;x+=8)if(x*x+y*y<420)g.FillEllipse(dots,x,y,3,3);
                    }
                    bitmap.Save(path,ImageFormat.Png);
                }
                _tool.ChoosePlanting("image");_tool.ClearArea();_tool.GoToStage(ToolStage.Design);
                _tool.SetValue("0","flagWidth","70");
                _tool.ApplyImportedImage(FlowerImageImporter.Read(path));
                var at=P3(st.S.Block.Center);
                yield return CameraTo(at+new float3(25,0,0),0,75,125,rec,null);
                FlowerPainterToolSystem.SuiteHover(at+new float3(8,0,0),at,12);
                yield return Feed(rec);yield return Frames(20);
                rec["placingImage"]=_tool.StateInput().PlacingFlag;
                if(!_tool.StateInput().PlacingFlag)rec["error"]="Image placement preview was not active";
                yield break;
            }
            if(view=="partition") {
                var partitions=_tool.StateInput().FloralPartitions;
                if(partitions==null || partitions.Regions.Count<2){rec["error"]="Need two regions for draft preservation";yield break;}
                int first=partitions.Regions[0].Id,second=partitions.Regions[1].Id;
                yield return UiAct(st,rec,"select first region","partition-select-"+first,"selectFloralRegion",new object[]{(double)first},()=>_tool.StateInput().FloralPartitions.Selected==first,()=>_tool.SelectFloralRegion(first));
                JObject result=null;
                yield return UiRun(SuiteUiSystem.ElementJs("fill-style-islands",click:true),r=>result=r);
                yield return Frames(5);
                yield return UiAct(st,rec,"select second region","partition-select-"+second,"selectFloralRegion",new object[]{(double)second},()=>_tool.StateInput().FloralPartitions.Selected==second,()=>_tool.SelectFloralRegion(second));
                yield return UiAct(st,rec,"return to first region","partition-select-"+first,"selectFloralRegion",new object[]{(double)first},()=>_tool.StateInput().FloralPartitions.Selected==first,()=>_tool.SelectFloralRegion(first));
                yield return UiRun(SuiteUiSystem.ElementJs("partition-unsaved",click:false),r=>result=r);
                rec["draftPreserved"]=(bool?)result?["value"]?["found"]==true;
                if(!(bool)rec["draftPreserved"])rec["error"]="Unapplied region draft did not survive switching regions";
            }
        }
    }
}
