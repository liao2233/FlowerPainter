using System;
using System.Linq;
using System.Threading;
using FlowerPainter.Core;
using FlowerPainter.Core.Geometry;
using FlowerPainter.Core.Layers.Types.Floral;
using Unity.Mathematics;
using Xunit;

namespace FlowerPainter.Tests
{
    public class FloralPatchTests
    {
        private static AreaPlan Plan(Action<SettingsBag> edit, float width=30, float depth=20)
        {
            var layer=new FloralLayer();var settings=layer.Defaults();settings.Values["plantingSystem"]="patch";edit(settings);
            return layer.Plan(new AreaInput { Settings=settings, Seed=42, Site=new SiteContext {HeightAt=p=>0},
                Shape=new AreaShape {Ring=new[]{new float3(0,0,0),new float3(width,0,0),new float3(width,0,depth),new float3(0,0,depth)}}},CancellationToken.None);
        }
        [Fact] public void DenseFlowersUsePatchesAndSimplerDistantMeshes()
        {
            var plan=Plan(s=>s.Values["shape"]="flag");
            int blooms=plan.Objects.Sum(o=>o.FlowerPatch.Flowers);
            Assert.InRange(blooms,7000,8000);Assert.InRange(plan.Objects.Count,35,70);
            Assert.True(blooms>100*plan.Objects.Count);
            Assert.All(plan.Objects,o=> {
                var m=o.FlowerPatch;
                Assert.StartsWith(FlowerPatchMesh.Prefix,o.Asset.Id);
                Assert.True(m.Lod.Indices.Length<m.Indices.Length/2);
                Assert.All(m.Vertices,p=>Assert.True(p.y>=0&&p.y<.4f));
                Assert.Equal(m.Vertices.Length,m.UV.Length);
                Assert.All(m.Indices,i=>Assert.InRange(i,0,m.Vertices.Length-1));
            });
        }
        [Fact] public void CurvedRegionClipsTheWholeMeshAndLeavesTheOutsideEmpty()
        {
            var plan=Plan(s=> {
                s.Values["shape"]="partition";
                s.Values["motifs"]=new[]{new FloralMotif {Id="circle",Kind="ring",Size=12}.Encode()};
                s.Values["fills"]=new[]{new FloralFill {X=0,Z=0,Style="single",Flower="Blue"}.Encode()};
            });
            Assert.NotEmpty(plan.Objects);
            foreach(var obj in plan.Objects) foreach(var p in obj.FlowerPatch.Vertices)
                Assert.True(math.distance((p+obj.Position).xz,new float2(15,10))<6.002f);
            Assert.All(plan.Objects.SelectMany(o=>o.FlowerPatch.UV).Select(uv=>(int)(uv.x*16)).Where(c=>c>=4),c=>Assert.Equal(6,c));
        }
        [Fact] public void ChunkEdgesDoNotShrinkFlowersOrDuplicateCentres()
        {
            var plan=Plan(s=>s.Values["flowerPalette"]=new[]{"White"},15.68f,15.68f);
            // 56 x 56 world-aligned blossom cells, including those alongside internal tile edges.
            Assert.Equal(56*56,plan.Objects.Sum(o=>o.FlowerPatch.Flowers));
            var full=plan.Objects.Where(o=>o.Position.x>0&&o.Position.x<8&&o.Position.z>0&&o.Position.z<8).ToArray();
            Assert.NotEmpty(full);
            Assert.All(full,o=>Assert.Equal(196,o.FlowerPatch.Flowers));
            Assert.Single(full.Select(o=>o.FlowerPatch.Name).Distinct());
        }
        [Fact] public void FlagsKeepTheirProportionsAndRecognizableColourRegions()
        {
            Assert.Equal(5,FlowerPatchPlanner.FlagColor("china",new float2(5f/30,1-5f/20)));
            Assert.Equal(5,FlowerPatchPlanner.FlagColor("china",new float2(10f/30,1-2f/20)));
            Assert.Equal(4,FlowerPatchPlanner.FlagColor("china",new float2(.8f,.5f)));
            Assert.Equal(4,FlowerPatchPlanner.FlagColor("japan",new float2(.5f,.5f)));
            Assert.Equal(7,FlowerPatchPlanner.FlagColor("japan",new float2(.2f,.5f)));
            Assert.Equal(new[]{15,7,4},new[]{.1f,.5f,.9f}.Select(x=>FlowerPatchPlanner.FlagColor("france",new float2(x,.5f))));
            var plan=Plan(s=>s.Values["shape"]="flag",30,30);
            var v=plan.Objects.SelectMany(o=>o.FlowerPatch.Vertices.Select(p=>p+o.Position)).ToArray();
            Assert.InRange(v.Min(p=>p.z),4.99f,5.01f);Assert.InRange(v.Max(p=>p.z),24.99f,25.01f);
        }
        [Fact] public void ReplanningProducesStableAssetNamesAndDensityDoesNotDependOnPropOverlap()
        {
            var first=Plan(s=>s.Values["overlap"]=0);
            var second=Plan(s=>s.Values["overlap"]=90);
            Assert.Equal(first.Objects.Select(o=>o.Asset.Id),second.Objects.Select(o=>o.Asset.Id));
            var sparse=Plan(s=>s.Values["bloomSpacing"]=.5);
            Assert.True(sparse.Objects.Sum(o=>o.FlowerPatch.Flowers)<first.Objects.Sum(o=>o.FlowerPatch.Flowers)/2);
        }
    }
}
