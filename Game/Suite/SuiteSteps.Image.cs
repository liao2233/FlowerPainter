using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Newtonsoft.Json.Linq;
using FlowerPainter.Core.Layers.Types.Floral;
using FlowerPainter.Core.Tool;
using FlowerPainter.Game.Assets;
using Unity.Mathematics;

namespace FlowerPainter.Game.Suite
{
    public sealed partial class SuiteRunner
    {
        private IEnumerator StepImportImage(ScenarioState st,JObject step,JObject rec)
        {
            _tool.ChoosePlanting("image");
            _tool.ClearArea();
            yield return UiGoToStage(rec,ToolStage.Design);
            yield return Frames(10);
            var formats=new[]{ImageFormat.Png,ImageFormat.Jpeg,ImageFormat.Bmp,ImageFormat.Gif,ImageFormat.Tiff};
            var checkedFormats=new JArray();FlowerImageImporter.Result imported=null;
            // An asymmetric image makes flipped axes, lost transparency and incorrect proportions visible.
            using(var bitmap=new Bitmap(240,160,PixelFormat.Format32bppArgb)) {
                using(var g=Graphics.FromImage(bitmap)) {
                    g.Clear(Color.Transparent);
                    g.FillRectangle(Brushes.Red,0,0,120,80);g.FillRectangle(Brushes.Yellow,120,0,120,80);
                    g.FillRectangle(Brushes.Blue,0,80,120,80);g.FillEllipse(Brushes.White,155,98,50,50);
                }
                for(int x=0;x<120;x++)for(int y=80;y<160;y++) {
                    int v=(int)Math.Round(x*255.0/119);
                    bitmap.SetPixel(x,y,y<120?Color.FromArgb(v,v,v):Color.FromArgb(255,128+v/2,102+v/2));
                }
                foreach(var format in formats) {
                    string path=Path.Combine(Path.GetTempPath(),"FlowerPainter-image-test-"+Guid.NewGuid().ToString("N")+"."+format.ToString().ToLowerInvariant());
                    try {
                        bitmap.Save(path,format);var result=FlowerImageImporter.Read(path);
                        var map=FlowerImage.Read(result.Data);
                        if(math.abs(map.Ratio-1.5f)>.001f || map.Color(new float2(.2f,.8f))!=FlowerImage.Nearest(255,0,0,255) || map.Color(new float2(.8f,.8f))!=FlowerImage.Nearest(255,255,0,255))
                            throw new InvalidDataException("Image colours or orientation changed: "+format);
                        if(format.Guid==ImageFormat.Png.Guid) {
                            if(map.Color(new float2(.98f,.02f))!=-1)throw new InvalidDataException("Transparent background was filled.");
                            imported=result;
                        }
                        checkedFormats.Add(format.ToString());
                    } finally {if(File.Exists(path))File.Delete(path);}
                }
                // Modern ICO embeds PNG, with a 22-byte icon directory.
                string iconPath=Path.Combine(Path.GetTempPath(),"FlowerPainter-image-test-"+Guid.NewGuid().ToString("N")+".ico");
                try {
                    using(var png=new MemoryStream()) {
                        bitmap.Save(png,ImageFormat.Png);var data=png.ToArray();
                        using var stream=File.Create(iconPath);using var w=new BinaryWriter(stream);
                        w.Write((ushort)0);w.Write((ushort)1);w.Write((ushort)1);w.Write((byte)240);w.Write((byte)160);
                        w.Write((byte)0);w.Write((byte)0);w.Write((ushort)1);w.Write((ushort)32);w.Write(data.Length);w.Write(22);w.Write(data);
                    }
                    FlowerImageImporter.Read(iconPath);checkedFormats.Add("Ico");
                } finally {if(File.Exists(iconPath))File.Delete(iconPath);}
            }
            imported.Name="Image flowers test.png";
            _tool.ChoosePlanting("flow");
            _tool.GoToStage(ToolStage.Area);
            var center=_tool.Cursor.xz;
            var ring=new float3[5];
            for(int i=0;i<5;i++){float angle=i*math.PI*2/5;ring[i]=new float3(center.x+12*math.cos(angle),0,center.y+12*math.sin(angle));}
            if(!_tool.SuiteDrawOutline(ring,out var issue))throw new InvalidOperationException("Workflow polygon: "+issue);
            _tool.ChoosePlanting("image");
            if(_tool.Shape!=null)throw new InvalidOperationException("Image reused the area's frame.");
            _tool.ApplyImportedImage(imported);
            if(!_tool.StateInput().PlacingFlag)throw new InvalidOperationException("Image did not start placement.");
            _tool.ChoosePlanting("flag");_tool.BeginFlagPlacement();
            _tool.ChoosePlanting("flow");
            if(_tool.StateInput().PlacingFlag || _tool.Shape?.Ring.Length!=5)throw new InvalidOperationException("Switch did not cancel placement and restore polygon draft.");
            _tool.ChoosePlanting("image");
            if(_tool.CurrentRecipe.Root.Settings.S("imageData","")!=imported.Data)throw new InvalidOperationException("Image draft lost.");
            _tool.BeginFlagPlacement();_tool.GoToStage(ToolStage.Area);
            if(_tool.StateInput().PlacingFlag)throw new InvalidOperationException("Stage change retained placement.");
            _tool.GoToStage(ToolStage.Design);_tool.BeginFlagPlacement();
            rec["workflowSwitches"]=true;
            rec["formats"]=checkedFormats;rec["sourceDeleted"]=true;
            rec["width"]=imported.Width;rec["height"]=imported.Height;
            yield return Frames(12);
        }
    }
}
