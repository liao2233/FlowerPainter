using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FlowerPainter.Core;
using FlowerPainter.Core.Geometry;
using FlowerPainter.Core.Layers.Common;
using FlowerPainter.Core.Layers.Types.Floral;
using Unity.Mathematics;
using Xunit;

namespace FlowerPainter.Tests
{
    public class FloralPartitionTests
    {
        private static readonly float3[] Frame = { new float3(0,0,0), new float3(100,0,0), new float3(100,0,100), new float3(0,0,100) };
        private static FloralMotif Circle(float size = 40) => new FloralMotif { Id="circle", Kind="ring", Size=size, Width=12, Flower="MustNotPlantRibbon" };
        private static FloralMotif Curve(params float2[] points) => new FloralMotif { Id="cut", Kind="curve", Size=100, Points=points, Width=20 };
        private static FloralPartitions Map(params FloralMotif[] cuts) => new FloralPartitions(LayerRegion.From(Frame), cuts.ToList(), CancellationToken.None);
        private static AreaInput Input(params FloralMotif[] cuts)
        {
            var settings = new FloralLayer().Defaults(); settings.Values["plantingSystem"]="props";
            settings.Values["shape"] = "partition";
            settings.Values["motifs"] = cuts.Select(m=>m.Encode()).ToArray();
            settings.Values["fillGaps"] = true; // A previous ribbon-mode setting cannot fill new subdivisions automatically.
            return new AreaInput { Shape=new AreaShape { Ring=Frame }, Settings=settings, Site=new SiteContext { HeightAt=p=>0 }, Seed=42 };
        }
        private static AreaPlan Plan(AreaInput input) => new FloralLayer().Plan(input, CancellationToken.None);

        [Fact] public void CirclesCreateAnInsideAndOutsideWithNoUnplantableRibbon()
        {
            var circle = Circle(); var map = Map(circle);
            Assert.Equal(2, map.Regions.Count);
            Assert.NotEqual(map.At(new float2(50,50)), map.At(new float2(5,5)));
            Assert.True(map.At(new float2(69.9f,50)) > 0);
            Assert.True(map.At(new float2(70.1f,50)) > 0);
            Assert.Equal(10000f, map.Regions.Sum(r=>r.Area), 1);
            circle.Width = 90;
            Assert.Equal(map.Regions.Select(r=>r.Area), Map(circle).Regions.Select(r=>r.Area));
        }
        [Fact] public void IntersectingCirclesAndCurvesCreateFourSelectableParts()
        {
            var map = Map(Circle(), Curve(new float2(-0.8f,0), new float2(0.8f,0)));
            Assert.Equal(4, map.Regions.Count);
            var labels = new[] { new float2(50,40), new float2(50,60), new float2(5,20), new float2(5,80) }.Select(map.At).ToArray();
            Assert.All(labels, label=>Assert.True(label>0)); Assert.Equal(4, labels.Distinct().Count());
            Assert.Equal(10000f, map.Regions.Sum(r=>r.Area), 1);
        }
        [Fact] public void NestedCirclesKeepTheirHolesAndCanBeFilledIndependently()
        {
            var map=Map(Circle(70),Circle(25));
            Assert.Equal(3,map.Regions.Count);
            Assert.Equal(3,new[]{new float2(50,50),new float2(75,50),new float2(5,5)}.Select(map.At).Distinct().Count());
        }
        [Fact] public void SeveralOpenCurvesCanTogetherEncloseARegion()
        {
            var a=Curve(new float2(-0.2f,-0.2f),new float2(0.2f,-0.2f),new float2(0,0.2f));
            var b=Curve(new float2(0,0.2f),new float2(-0.2f,-0.2f));
            Assert.Single(Map(a).Regions); Assert.Single(Map(b).Regions);
            Assert.Equal(2,Map(a,b).Regions.Count);
        }
        [Fact] public void DrawingBoundariesAlonePlantsNothingAndWaitsForARegionFill()
        {
            var input=Input(Circle());
            Assert.Empty(Plan(input).Objects);
            Assert.False(new FloralLayer().CheckReady(input).Ready);
            input.Settings.Values["fills"]=new[]{new FloralFill{X=0,Z=0,Style="single",Flower="Inside"}.Encode()};
            Assert.True(new FloralLayer().CheckReady(input).Ready);
            var plan=Plan(input); Assert.NotEmpty(plan.Objects);
            Assert.All(plan.Objects,o=>{Assert.Equal("Inside",o.Asset.Id);Assert.True(math.distance(o.Position.xz,new float2(50,50))<=20.01f);});
            Assert.DoesNotContain(plan.Objects,o=>o.Control=="motifs");
        }
        [Fact] public void FillingBothSidesCoversTheFormerRibbonAndRebuildingTheCircleRepartitions()
        {
            var circle=Circle(); var input=Input(circle);
            input.Settings.Values["fills"]=new[]{new FloralFill{Style="single",Flower="Inside"}.Encode(),new FloralFill{X=-40,Z=-40,Style="single",Flower="Outside"}.Encode()};
            var plan=Plan(input);
            Assert.Contains(plan.Objects,o=>o.Asset.Id=="Outside");
            Assert.Contains(plan.Objects,o=>math.abs(math.distance(o.Position.xz,new float2(50,50))-20)<0.3f);
            Assert.DoesNotContain(plan.Objects,o=>o.Asset.Id=="MustNotPlantRibbon");
            circle.Size=60; input.Settings.Values["motifs"]=new[]{circle.Encode()};
            var expanded=Plan(input);
            Assert.True(expanded.Objects.Count(o=>o.Asset.Id=="Inside")>plan.Objects.Count(o=>o.Asset.Id=="Inside")*2);
            input.Settings.Values["fills"]=new[]{new FloralFill{Style="empty"}.Encode(),new FloralFill{X=-40,Z=-40,Style="single",Flower="Outside"}.Encode()};
            Assert.All(Plan(input).Objects,o=>Assert.True(math.distance(o.Position.xz,new float2(50,50))>=29.9f));
        }
        [Fact] public void PartitionConstructionHonoursCancellation()
        {
            using var cancel=new CancellationTokenSource(); cancel.Cancel();
            Assert.Throws<OperationCanceledException>(()=>new FloralPartitions(LayerRegion.From(Frame),new List<FloralMotif>{Circle()},cancel.Token));
        }

        [Fact] public void RegionPalettesRoundTripWithoutChangingLegacyFills()
        {
            var legacy = new FloralFill {X=4,Z=-6,Style="single",Flower="Old|Flower"};
            Assert.Equal(legacy.Encode(), FloralFill.Read(new[]{legacy.Encode()})[0].Encode());
            var fill = new FloralFill {X=-2,Z=3,Style="flow",Flowers=new[]{"Red,Flower", "Blue|花%"}};
            var restored = FloralFill.Read(new[]{fill.Encode()})[0];
            Assert.Equal(fill.Flowers, restored.Flowers); Assert.Equal(fill.Style, restored.Style);
            Assert.Equal(fill.X, restored.X); Assert.Equal(fill.Z, restored.Z);
        }

        [Theory]
        [InlineData("flow")]
        [InlineData("islands")]
        public void EachSubdivisionUsesItsOwnPaletteAndStyle(string style)
        {
            var input=Input(Circle(60));
            var inside=new FloralFill {Style=style,Flowers=new[]{"Red","Yellow"}};
            var outside=new FloralFill {X=-40,Z=-40,Style="single",Flowers=new[]{"Blue"}};
            input.Settings.Values["fills"]=new[]{inside.Encode(),outside.Encode()};
            input.Settings.Values["flowerPalette"]=new[]{"MustNotUseGlobal"};
            var map=Map(Circle(60));int inner=map.At(new float2(50,50));
            var plan=Plan(input);
            var a=plan.Objects.Where(o=>map.At(o.Position.xz)==inner).ToList();
            Assert.Equal(2,a.Select(o=>o.Asset.Id).Distinct().Count());
            Assert.All(a,o=>Assert.Contains(o.Asset.Id,inside.Flowers));
            Assert.All(plan.Objects.Where(o=>map.At(o.Position.xz)!=inner),o=>Assert.Equal("Blue",o.Asset.Id));
            inside.Style="single";inside.Flowers=new[]{"White"};
            input.Settings.Values["fills"]=new[]{inside.Encode(),outside.Encode()};
            var changed=Plan(input);
            Assert.All(changed.Objects.Where(o=>map.At(o.Position.xz)==inner),o=>Assert.Equal("White",o.Asset.Id));
            Assert.Equal(plan.Objects.Where(o=>map.At(o.Position.xz)!=inner).Select(o=>(o.Position,o.Asset.Id)),changed.Objects.Where(o=>map.At(o.Position.xz)!=inner).Select(o=>(o.Position,o.Asset.Id)));
        }
    }
}
