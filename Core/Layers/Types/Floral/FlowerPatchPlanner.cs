using System;
using System.Collections.Generic;
using FlowerPainter.Core.Geometry;
using FlowerPainter.Core.Layers.Common;
using Unity.Mathematics;

namespace FlowerPainter.Core.Layers.Types.Floral
{
    internal static class FlowerPatchPlanner
    {
        public static void Plan(LayerContext ctx)
        {
            var frame=ctx.LayerRegion;var shape=ctx.S("shape");var origin=FloralMotif.Origin(frame);
            var motifs=FloralMotif.Read(ctx.L("motifs"));var fills=FloralFill.Read(ctx.L("fills"));
            var regions=new List<(Region region,FloralFill fill)>();
            bool flag=shape=="flag";
            if(shape=="partition") {
                var map=new FloralPartitions(frame,motifs,ctx.Cancel);
                var choices=new Dictionary<int,FloralFill>();
                foreach(var f in fills) {int id=map.At(origin+new float2(f.X,f.Z));if(id>0)choices[id]=f;}
                for(int i=0;i<map.Regions.Count;i++) if(choices.TryGetValue(i+1,out var fill)&&fill.Style!="empty") regions.Add((map.Regions[i],fill));
                if(regions.Count==0){ctx.Issue(IssueSeverity.Info,"FlowerPainter.Floral.Issue.FillPartition");return;}
            } else regions.Add((new Region(frame.Ring),null));
            var palette=ctx.L("flowerPalette");if(palette.Length==0)palette=new[]{"Red","Yellow","White"};
            float pitch=math.clamp(ctx.F("bloomSpacing"),0.22f,0.5f),height=math.clamp(ctx.F("patchHeight"),0.12f,0.6f);
            int flowers=0, triangles=0;float tile=14*pitch;
            // Flags retain their 3:2 footprint and are centred in the selection.
            var span=frame.Max-frame.Min;float flagW=math.min(span.x,span.y*1.5f),flagH=flagW/1.5f;
            var flagMin=origin-new float2(flagW,flagH)*0.5f;
            foreach(var item in regions) {
                var selected=item.fill?.Flowers.Length>0?item.fill.Flowers:item.fill?.Style=="single"&&!string.IsNullOrEmpty(item.fill.Flower)?new[]{item.fill.Flower}:palette;
                string style=item.fill?.Style??(shape=="islands"?"islands":"flow");
                var pattern=new FloralCarpet.Pattern(frame,style,ctx.F("bandWidth"),ctx.F("rotation"),ctx.Seed,selected.Length);
                int Color(float2 p) {
                    if(ctx.Input.Reserved?.Blocks(p,0.05f)??false)return -1;
                    if(flag)return FlagColor(ctx.S("flagTemplate"),(p-flagMin)/new float2(flagW,flagH));
                    return FlowerPatchMesh.ColorOf(selected[style=="single"?0:pattern.Slot(p)]);
                }
                var region=item.region;
                float Clearance(float2 p) {
                    float d=region.DistanceToBoundary(p);
                    if(flag) d=math.min(d,math.cmin(math.min(p-flagMin,flagMin+new float2(flagW,flagH)-p)));
                    return d;
                }
                Poly.Bounds(region.Outer,out var min,out var max);
                for(float z=math.floor(min.y/tile)*tile;z<max.y;z+=tile)for(float x=math.floor(min.x/tile)*tile;x<max.x;x+=tile) {
                    ctx.Cancel.ThrowIfCancellationRequested();
                    var lo=new float2(x,z);var hi=lo+tile;
                    if(flag){lo=math.max(lo,flagMin);hi=math.min(hi,flagMin+new float2(flagW,flagH));if(math.any(hi<=lo))continue;}
                    var pieces=Poly.Boolean(new[]{region},new[]{new Region(Rect(lo,hi))},BoolOp.Intersection);
                    if(pieces.Count==0)continue;
                    var pos=new float3(x,ctx.H(new float2(x,z)),z);
                    var mesh=FlowerPatchMesh.Make(pieces,pos,pitch,height,Color,ctx.H,Clearance,ctx.Cancel);
                    if(mesh.Flowers==0)continue;
                    var obj=ctx.Object(pos.xz,0,ObjectRole.Planter,new AssetRef(mesh.Name),0,-1,AssetCategories.Flower);
                    obj.Position=pos;obj.Control="plantingSystem";obj.FlowerPatch=mesh;
                    flowers+=mesh.Flowers;triangles+=mesh.Indices.Length/3;
                }
            }
            ctx.Stat("FlowerPainter.Floral.Stat.PatchFlowers",flowers,Keys.UnitCount);
            ctx.Stat("FlowerPainter.Floral.Stat.Patches",ctx.Plan.Objects.Count,Keys.UnitCount);
            ctx.Plan.Facts["floral.patchFlowers"]=flowers;ctx.Plan.Facts["floral.patches"]=ctx.Plan.Objects.Count;
            ctx.Plan.Facts["floral.patchTriangles"]=triangles;ctx.Plan.Facts["floral.overlap"]=0;
        }
        private static float2[] Rect(float2 a,float2 b)=>new[]{a,new float2(b.x,a.y),b,new float2(a.x,b.y)};

        // China: 30x20 construction grid; all four small stars point towards the large star.
        // Source: https://www.gov.cn/xinwen/2020-12/24/5572913/files/ced32b3ff3644aa4bfe841b71050345f.pdf
        internal static int FlagColor(string flag,float2 uv)
        {
            if(math.any(uv<0)||math.any(uv>1))return -1;
            if(flag=="japan")return math.length((uv-new float2(0.5f))*new float2(1.5f,1))<=0.3f?4:7;
            if(flag=="france")return uv.x<1f/3?15:uv.x<2f/3?7:4;
            var p=new float2(uv.x*30,(1-uv.y)*20);
            var big=new float2(5,5);
            if(Star(p,big,3,-math.PI/2))return 5;
            foreach(var c in new[]{new float2(10,2),new float2(12,4),new float2(12,7),new float2(10,9)})
                if(Star(p,c,1,math.atan2(big.y-c.y,big.x-c.x)))return 5;
            return 4;
        }
        private static bool Star(float2 p,float2 c,float r,float angle)
        {
            if(math.distance(p,c)>r)return false;
            var points=new float2[10];for(int i=0;i<10;i++){float a=angle+i*math.PI/5;points[i]=c+new float2(math.cos(a),math.sin(a))*r*((i&1)==0?1:0.381966f);}
            return Poly.Contains(points,p);
        }
    }
}
