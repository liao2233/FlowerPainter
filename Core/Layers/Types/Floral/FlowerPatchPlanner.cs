using System;
using System.Collections.Generic;
using System.Linq;
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
                var partitionChoices=new Dictionary<int,FloralFill>();
                foreach(var f in fills) {int id=map.At(origin+new float2(f.X,f.Z));if(id>0)partitionChoices[id]=f;}
                for(int i=0;i<map.Regions.Count;i++) if(partitionChoices.TryGetValue(i+1,out var fill)&&fill.Style!="empty") regions.Add((map.Regions[i],fill));
                if(regions.Count==0){ctx.Issue(IssueSeverity.Info,"FlowerPainter.Floral.Issue.FillPartition");return;}
            } else regions.Add((new Region(frame.Ring),null));
            var palette=ctx.L("flowerPalette");if(palette.Length==0)palette=new[]{"Red","Yellow","White"};
            float pitch=math.clamp(ctx.F("bloomSpacing"),0.22f,0.5f),height=math.clamp(ctx.F("patchHeight"),0.12f,0.6f);
            int density=math.clamp((int)ctx.F("flowerDensity"),1,4);
            int flowers=0, triangles=0,lodTriangles=0;float tile=FlowerPatchMesh.TilePitches*pitch;
            var meshes=new Dictionary<string,FlowerPatchMesh>();
            // Each template retains its own aspect ratio, including square and non-rectangular flags.
            var template=FlagCatalog.Get(ctx.S("flagTemplate"));
            var dimensions=template.Fit(frame.Max-frame.Min);float flagW=dimensions.x,flagH=dimensions.y;
            var flagMin=origin-new float2(flagW,flagH)*0.5f;
            Region flagRegion=null;
            if(flag) {
                flagRegion=new Region(template.Outline.Length>0?template.Outline.Select(p=>flagMin+p*dimensions).ToArray():Rect(flagMin,flagMin+dimensions));
                regions=Poly.Boolean(regions.Select(r=>r.region).ToArray(),new[]{flagRegion},BoolOp.Intersection)
                    .Select(r=>(r,(FloralFill)null)).ToList();
                float recommended=template.RecommendedColumns*pitch;
                ctx.Stat("FlowerPainter.Floral.Stat.FlagWidth",flagW,"m");
                ctx.Stat("FlowerPainter.Floral.Stat.FlagHeight",flagH,"m");
                ctx.Plan.Facts["floral.flagWidth"]=flagW;ctx.Plan.Facts["floral.flagHeight"]=flagH;
                ctx.Plan.Facts["floral.flagRecommendedWidth"]=recommended;
                if(flagW<recommended)ctx.Issue(IssueSeverity.Warning,"FlowerPainter.Floral.Issue.FlagSmall",
                    Math.Round(flagW,1),Math.Round(flagH,1),Math.Round(recommended,1),Math.Round(recommended/template.Ratio,1));
                if(regions.Sum(r=>r.region.Area)<flagRegion.Area*.98f)
                    ctx.Issue(IssueSeverity.Warning,"FlowerPainter.Floral.Issue.FlagClipped");
            }
            bool collage=shape=="collage";
            var regionMap=collage && fills.Count>0 ? new FloralRegions(frame,motifs,ctx.Cancel) : null;
            var choices=new Dictionary<int,FloralFill>();
            if(regionMap!=null)foreach(var fill in fills) {
                int label=regionMap.At(origin+new float2(fill.X,fill.Z));
                if(label>0)choices[label]=fill;
            }
            var curvePalette=ctx.L("curvePalette");
            if(curvePalette.Length==0)curvePalette=FloralAssets.CurveDefaults(ctx.Site);
            var fillPatterns=new Dictionary<FloralFill,FloralCarpet.Pattern>();
            foreach(var fill in choices.Values) {
                int count=fill.Flowers.Length>0?fill.Flowers.Length:palette.Length;
                fillPatterns[fill]=new FloralCarpet.Pattern(frame,fill.Style,ctx.F("bandWidth"),ctx.F("rotation"),ctx.Seed,count);
            }
            foreach(var item in regions) {
                var selected=item.fill?.Flowers.Length>0?item.fill.Flowers:item.fill?.Style=="single"&&!string.IsNullOrEmpty(item.fill.Flower)?new[]{item.fill.Flower}:palette;
                string style=item.fill?.Style??(collage?ctx.S("gapStyle"):(shape=="islands"?"islands":"flow"));
                var pattern=new FloralCarpet.Pattern(frame,style,ctx.F("bandWidth"),ctx.F("rotation"),ctx.Seed,selected.Length);
                int Color(float2 p) {
                    if(ctx.Input.Reserved?.Blocks(p,0.05f)??false)return -1;
                    if(flag)return template.Color((p-flagMin)/dimensions);
                    if(collage) {
                        for(int i=motifs.Count-1;i>=0;i--)if(motifs[i].Covers(p,origin))
                            return FlowerPatchMesh.ColorOf(string.IsNullOrEmpty(motifs[i].Flower)?curvePalette[i%curvePalette.Length]:motifs[i].Flower);
                        FloralFill fill=null;
                        if(regionMap!=null)choices.TryGetValue(regionMap.At(p),out fill);
                        if(fill?.Style=="empty" || (fill==null&&!ctx.B("fillGaps")))return -1;
                        if(fill!=null) {
                            var colors=fill.Flowers.Length>0?fill.Flowers:fill.Style=="single"&&!string.IsNullOrEmpty(fill.Flower)?new[]{fill.Flower}:palette;
                            return FlowerPatchMesh.ColorOf(colors[fill.Style=="single"?0:fillPatterns[fill].Slot(p)]);
                        }
                    }
                    return FlowerPatchMesh.ColorOf(selected[style=="single"?0:pattern.Slot(p)]);
                }
                var region=item.region;
                float Clearance(float2 p) {
                    float d=region.DistanceToBoundary(p);
                    if(flag) d=math.min(d,math.cmin(math.min(p-flagMin,flagMin+new float2(flagW,flagH)-p)));
                    return d;
                }
                Poly.Bounds(region.Outer,out var min,out var max);
                for(int tz=(int)math.floor(min.y/tile);tz*tile<max.y;tz++)for(int tx=(int)math.floor(min.x/tile);tx*tile<max.x;tx++) {
                    ctx.Cancel.ThrowIfCancellationRequested();
                    float x=tx*tile,z=tz*tile;
                    var lo=new float2(x,z);var hi=lo+tile;
                    if(flag){lo=math.max(lo,flagMin);hi=math.min(hi,flagMin+new float2(flagW,flagH));if(math.any(hi<=lo))continue;}
                    var pieces=Poly.Boolean(new[]{region},new[]{new Region(Rect(lo,hi))},BoolOp.Intersection);
                    if(pieces.Count==0)continue;
                    var pos=new float3(x,ctx.H(new float2(x,z)),z);
                    FlowerPatchMesh mesh;
                    mesh=FlowerPatchMesh.Make(pieces,pos,pitch,height,density,Color,ctx.H,Clearance,ctx.Cancel,meshes,true);
                    if(mesh.Indices.Length==0)continue;
                    var obj=ctx.Object(pos.xz,0,ObjectRole.Planter,new AssetRef(mesh.Name),0,-1,AssetCategories.Flower);
                    obj.Position=pos;obj.Control="plantingSystem";obj.FlowerPatch=mesh;
                    flowers+=mesh.Flowers;triangles+=mesh.Indices.Length/3;lodTriangles+=mesh.Lod.Indices.Length/3;
                }
            }
            ctx.Stat("FlowerPainter.Floral.Stat.Patches",ctx.Plan.Objects.Count,Keys.UnitCount);
            ctx.Plan.Facts["floral.patchFlowers"]=flowers;ctx.Plan.Facts["floral.patches"]=ctx.Plan.Objects.Count;
            ctx.Plan.Facts["floral.patchTriangles"]=triangles;ctx.Plan.Facts["floral.overlap"]=0;
            ctx.Plan.Facts["floral.patchLodTriangles"]=lodTriangles;

        }
        private static float2[] Rect(float2 a,float2 b)=>new[]{a,new float2(b.x,a.y),b,new float2(a.x,b.y)};

        internal static int FlagColor(string flag,float2 uv)=>FlagCatalog.Get(flag).Color(uv);
    }
}
