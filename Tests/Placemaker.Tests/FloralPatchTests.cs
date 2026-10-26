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
        private static AreaPlan Plan(Action<SettingsBag> edit, float width=30, float depth=20, float2 offset=default)
        {
            var layer=new FloralLayer();var settings=layer.Defaults();settings.Values["plantingSystem"]="patch";edit(settings);
            return layer.Plan(new AreaInput { Settings=settings, Seed=42, Site=new SiteContext {HeightAt=p=>0},
                Shape=new AreaShape {Ring=new[]{new float3(offset.x,0,offset.y),new float3(offset.x+width,0,offset.y),new float3(offset.x+width,0,offset.y+depth),new float3(offset.x,0,offset.y+depth)}}},CancellationToken.None);
        }
        [Fact] public void LargeFlagSharesRepeatedTilePayloadsAwayFromWorldOrigin()
        {
            var plan=Plan(s=>s.Values["shape"]="flag",180,140,new float2(1958.5f,-80));
            Assert.InRange(plan.Objects.Sum(o=>o.FlowerPatch.Flowers),270000,280000);
            var meshes=plan.Objects.Select(o=>o.FlowerPatch).Distinct().ToArray();
            Assert.True(meshes.Length<plan.Objects.Count/3,$"{meshes.Length} meshes for {plan.Objects.Count} tiles");
            Assert.Equal(meshes.Length,meshes.Select(m=>m.Name).Distinct().Count());
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
        [Fact] public void CatalogueCoversEveryUNMemberAndObserverAndAllMapsDecode()
        {
            // 193 member states + Holy See and Palestine; territories are additional entries.
            var nations="af al dz ad ao ag ar am au at az bs bh bd bb by be bz bj bt bo ba bw br bn bg bf bi cv kh cm ca cf td cl cn co km cg cd cr ci hr cu cy cz dk dj dm do ec eg sv gq er ee sz et fj fi fr ga gm ge de gh gr gd gt gn gw gy ht hn hu is in id ir iq ie il it jm jp jo kz ke ki kp kr kw kg la lv lb ls lr ly li lt lu mg mw my mv ml mt mh mr mu mx fm md mc mn me ma mz mm na nr np nl nz ni ne ng mk no om pk pw pa pg py pe ph pl pt qa ro ru rw kn lc vc ws sm st sa sn rs sc sl sg sk si sb so za ss es lk sd sr se ch sy tj tz th tl tg to tt tn tr tm tv ug ua ae gb us uy uz vu ve vn ye zm zw va ps".Split(' ');
            Assert.Equal(195,nations.Length);
            var ids=FlagCatalog.All.Select(f=>f.Id).ToArray();Assert.Equal(ids.Length,ids.Distinct().Count());
            foreach(var code in nations)Assert.Contains(code=="cn"?"china":code=="jp"?"japan":code=="fr"?"france":code,ids);
            foreach(var code in new[]{"un","who","wto","eu","nato","au-union","red-cross","red-crescent","red-crystal","first-aid","medical","peace","rainbow","checkered"})Assert.Contains(code,ids);
            foreach(var f in FlagCatalog.All) {
                Assert.InRange(f.Ratio,.5f,3f);bool visible=false;
                for(int y=0;y<32;y++)for(int x=0;x<32;x++) {int c=f.Color(new float2((x+.5f)/32,(y+.5f)/32));Assert.True(c==-1||c>=4&&c<16);visible|=c>=0;}
                Assert.True(visible,f.Id);Assert.Equal(-1,f.Color(new float2(-.1f,.5f)));
            }
        }
        [Fact] public void FlagsKeepSquareWideAndNonRectangularOutlines()
        {
            Assert.Equal(new float2(20,20),FlagCatalog.Get("ch").Fit(new float2(30,20)));
            Assert.Equal(new float2(30,15),FlagCatalog.Get("gb").Fit(new float2(30,20)));
            Assert.Equal(-1,FlagCatalog.Get("np").Color(new float2(.95f,.8f)));
            var plan=Plan(s=>{s.Values["shape"]="flag";s.Values["flagTemplate"]="np";});
            var template=FlagCatalog.Get("np");var size=template.Fit(new float2(30,20));var min=new float2(15,10)-size/2;
            var region=new Region(template.Outline.Select(p=>min+p*size).ToArray());
            Assert.NotEmpty(plan.Objects);
            foreach(var obj in plan.Objects)foreach(var v in obj.FlowerPatch.Vertices) {
                var p=(obj.Position+v).xz;Assert.True(region.Contains(p)||region.DistanceToBoundary(p)<.003f);
            }
        }
        [Fact] public void SizeAdviceTracksTemplateDetailAndSpacingWithoutBlockingSmallFlags()
        {
            var small=Plan(s=>{s.Values["shape"]="flag";s.Values["flagTemplate"]="us";},10,8);
            Assert.NotEmpty(small.Objects);Assert.Contains(small.Issues,i=>i.Key=="FlowerPainter.Floral.Issue.FlagSmall");
            Assert.DoesNotContain(small.Issues,i=>i.Severity==IssueSeverity.Error);
            var dense=Plan(s=>{s.Values["shape"]="flag";s.Values["flagTemplate"]="us";s.Values["bloomSpacing"]=.22;},10,8);
            Assert.True(dense.Facts["floral.flagRecommendedWidth"]<small.Facts["floral.flagRecommendedWidth"]);
            var simple=Plan(s=>{s.Values["shape"]="flag";s.Values["flagTemplate"]="france";},20,15);
            Assert.DoesNotContain(simple.Issues,i=>i.Key=="FlowerPainter.Floral.Issue.FlagSmall");
        }
        [Theory]
        [InlineData("kr")][InlineData("us")][InlineData("br")][InlineData("un")][InlineData("wto")][InlineData("who")][InlineData("red-cross")]
        public void DetailedAndThemeFlagsActuallyProducePatchGeometry(string flag)
        {
            var plan=Plan(s=>{s.Values["shape"]="flag";s.Values["flagTemplate"]=flag;},12,10);
            Assert.NotEmpty(plan.Objects);Assert.True(plan.Objects.Sum(o=>o.FlowerPatch.Flowers)>500);
            Assert.True(plan.Objects.SelectMany(o=>o.FlowerPatch.UV).Select(p=>(int)(p.x*16)).Where(c=>c>=4).Distinct().Count()>=2);
        }
    }
}
