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
        private static AreaPlan Plan(Action<SettingsBag> edit, float width=30, float depth=20, float2 offset=default, Func<float2,float> terrain=null)
        {
            var layer=new FloralLayer();var settings=layer.Defaults();settings.Values["plantingSystem"]="patch";settings.Values["flowerDensity"]=1;edit(settings);
            return layer.Plan(new AreaInput { Settings=settings, Seed=42, Site=new SiteContext {HeightAt=terrain??(p=>0)},
                Shape=new AreaShape {Ring=new[]{new float3(offset.x,0,offset.y),new float3(offset.x+width,0,offset.y),new float3(offset.x+width,0,offset.y+depth),new float3(offset.x,0,offset.y+depth)}}},CancellationToken.None);
        }
        [Theory]
        [InlineData("flag")][InlineData("flow")]
        public void PatchTrianglesFollowHillsWithoutCoveringFlowersInValleys(string shape)
        {
            float Height(float2 p) => 2f*math.sin(p.x*math.PI/10)*math.cos(p.y*math.PI/10);
            var plan=Plan(s=>s.Values["shape"]=shape,terrain:Height);
            Assert.NotEmpty(plan.Objects);
            foreach(var obj in plan.Objects) foreach(var mesh in new[]{obj.FlowerPatch,obj.FlowerPatch.Lod})
                for(int i=0;i<mesh.Indices.Length;i+=3) {
                    var center=obj.Position+(mesh.Vertices[mesh.Indices[i]]+mesh.Vertices[mesh.Indices[i+1]]+mesh.Vertices[mesh.Indices[i+2]])/3;
                    Assert.InRange(center.y-Height(center.xz),-.1f,.4f);
                }
        }
        [Theory]
        [InlineData("flag")][InlineData("flow")]
        public void FlowerDensityControlsRaisedFlowers(string shape)
        {
            var counts=Enumerable.Range(1,4).Select(d=>Plan(s=>{s.Values["shape"]=shape;s.Values["flowerDensity"]=d;},12,10)
                .Objects.Sum(o=>o.FlowerPatch.Flowers)).ToArray();
            Assert.True(counts[0]>0);
            for(int i=1;i<counts.Length;i++)Assert.True(counts[i]>counts[i-1],string.Join(",",counts));
        }
        [Fact] public void FlagNearGeometryContainsOnlyFlowersInTemplateColors()
        {
            var plan=Plan(s=>{s.Values["shape"]="flag";s.Values["flowerDensity"]=4;},30,20);
            Assert.True(plan.Objects.Sum(o=>o.FlowerPatch.Flowers)>1000);
            foreach(var obj in plan.Objects) {
                var mesh=obj.FlowerPatch;
                Assert.Equal(mesh.Flowers*6,mesh.Indices.Length/3);
                Assert.All(mesh.UV,uv=>Assert.True(uv.y>=.5f));
            }
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
        [Theory]
        [InlineData("flow")][InlineData("islands")][InlineData("partition")]
        public void FilledRegionsUseTheSameRaisedFlowersAsFlags(string shape)
        {
            var plan=Plan(s=>{
                s.Values["shape"]=shape;s.Values["flowerDensity"]=4;
                if(shape=="partition")s.Values["fills"]=new[]{new FloralFill { X=0,Z=0,Style="single",Flower="Blue" }.Encode()};
            },20,20);
            Assert.NotEmpty(plan.Objects);
            Assert.All(plan.Objects,o=>Assert.Equal(o.FlowerPatch.Flowers*6,o.FlowerPatch.Indices.Length/3));
        }
        [Fact] public void CurvesAndTheirSelectedFillsUseRaisedFlowersAndRemainIndependent()
        {
            var motif=new FloralMotif {Id="curve",Kind="wave",Size=16,Width=2,Flower="Red"};
            var plan=Plan(s=>{
                s.Values["shape"]="collage";s.Values["flowerDensity"]=4;
                s.Values["motifs"]=new[]{motif.Encode()};
                s.Values["fills"]=new[]{new FloralFill {X=0,Z=7,Style="single",Flower="Blue"}.Encode()};
            },20,20);
            Assert.NotEmpty(plan.Objects);
            var colors=plan.Objects.SelectMany(o=>o.FlowerPatch.UV).Where(uv=>uv.y>=.5f).Select(uv=>(int)(uv.x*16)).ToArray();
            Assert.Contains(4,colors);Assert.Contains(6,colors);
            Assert.All(plan.Objects,o=>Assert.Equal(o.FlowerPatch.Flowers*6,o.FlowerPatch.Indices.Length/3));
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
            var v=plan.Objects.SelectMany(o=>o.FlowerPatch.Lod.Vertices.Select(p=>p+o.Position)).ToArray();
            Assert.InRange(v.Min(p=>p.z),4.99f,5.01f);Assert.InRange(v.Max(p=>p.z),24.99f,25.01f);
            Assert.True(plan.Objects.Sum(o=>o.FlowerPatch.Flowers)>0);
        }
        [Fact] public void ReplanningProducesStableAssetNamesAndDensityDoesNotDependOnPropOverlap()
        {
            var first=Plan(s=>{s.Values["overlap"]=0;s.Values["flowerDensity"]=1;});
            var second=Plan(s=>{s.Values["overlap"]=90;s.Values["flowerDensity"]=1;});
            Assert.Equal(first.Objects.Select(o=>o.Asset.Id),second.Objects.Select(o=>o.Asset.Id));
            var sparse=Plan(s=>{s.Values["bloomSpacing"]=.5;s.Values["flowerDensity"]=1;});
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
        [InlineData("china")][InlineData("japan")][InlineData("france")]
        public void DetailedAndThemeFlagsActuallyProducePatchGeometry(string flag)
        {
            var plan=Plan(s=>{s.Values["shape"]="flag";s.Values["flagTemplate"]=flag;},12,10);
            Assert.NotEmpty(plan.Objects);Assert.All(plan.Objects,o=>Assert.NotEmpty(o.FlowerPatch.Indices));
            Assert.True(plan.Objects.Sum(o=>o.FlowerPatch.Flowers)>0);
            Assert.All(plan.Objects,o=>Assert.Null(o.FlowerPatch.FlagId));
        }
        [Theory]
        [InlineData("flag")][InlineData("flow")]
        public void LargeCanopiesAvoidMillionFlowerGeometry(string shape)
        {
            var plan=Plan(s=>s.Values["shape"]=shape,319,313,terrain:p=>.3f*math.sin(p.x*math.PI/50)*math.cos(p.y*math.PI/50));
            Assert.InRange(plan.Objects.Count,1,400);
            int blooms=plan.Objects.Sum(o=>o.FlowerPatch.Flowers);
            Assert.InRange(blooms,1,25000);
            Assert.InRange(plan.Objects.Sum(o=>o.FlowerPatch.Indices.Length/3),1,600000);
            Assert.True(plan.Objects.Sum(o=>o.FlowerPatch.Lod.Indices.Length)<=plan.Objects.Sum(o=>o.FlowerPatch.Indices.Length));
        }
    }
}
