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
    public class FlowerWorkflowTests
    {
        private static float3[] Frame() => new[] {new float3(0,0,0),new float3(100,0,0),new float3(100,0,100),new float3(0,0,100)};
        [Fact] public void TheStandaloneCatalogOnlyOffersFlowers() { Assert.Equal("floral",Assert.Single(GeneratorRegistry.All).Id); }
        [Fact] public void FlowersDoNotCreateAFunctionalPark() {
            var recipe = new Recipe { Root = new RecipeStep { Layer="floral" } };
            Assert.Equal(FlowerPainter.Core.Build.ParkStatsKind.NotApplicable,FlowerPainter.Core.Build.ParkStatsInput.KindOf(recipe,out _));
        }
        [Fact] public void CurvePointsRoundTripAndScalingDoesNotChangeWidth() {
            var m=new FloralMotif {Id="free",Kind="curve",Size=80,Width=3,Points=new[]{new float2(-0.7f,0),new float2(0,0.3f),new float2(0.6f,0)}};
            var parsed=FloralMotif.Parse(m.Encode()); Assert.NotNull(parsed); Assert.Equal(m.Points,parsed.Points);
            var larger=parsed.Clone(); larger.Size*=2;
            Assert.Equal(parsed.Width,larger.Width); Assert.True(math.distance(larger.Outline(float2.zero)[0],parsed.Outline(float2.zero)[0])>10);
            m.Points=Array.Empty<float2>(); Assert.NotNull(FloralMotif.Parse(m.Encode())); Assert.False(m.Covers(float2.zero,float2.zero));
        }
        [Fact] public void HeartHasAnEditableOutline() {
            var m=new FloralMotif{Kind="heart",Size=40,Width=3,Angle=25};
            Assert.All(m.Outline(new float2(50,50)),p=>Assert.True(m.Covers(p,new float2(50,50))));
            Assert.False(m.Covers(new float2(50,50),new float2(50,50)));
        }
        [Fact] public void CircleInteriorAndExteriorAreDifferentFillRegions() {
            var region=LayerRegion.From(Frame()); var ring=new FloralMotif{Kind="ring",Size=50,Width=4};
            var map=new FloralRegions(region,new List<FloralMotif>{ring},CancellationToken.None);
            Assert.True(map.At(new float2(50,50))>0); Assert.True(map.At(new float2(5,5))>0);
            Assert.NotEqual(map.At(new float2(50,50)),map.At(new float2(5,5)));
        }
        [Fact] public void ACurveCanDivideTheFrameWhileExtendingOutsideIt() {
            var region=LayerRegion.From(Frame()); var m=new FloralMotif{Kind="curve",Size=100,Width=5,Points=new[]{new float2(-0.8f,0),new float2(0.8f,0)}};
            var map=new FloralRegions(region,new List<FloralMotif>{m},CancellationToken.None);
            Assert.NotEqual(map.At(new float2(50,20)),map.At(new float2(50,80)));
            Assert.True(m.Covers(new float2(-10,50),new float2(50,50)));
        }
        [Fact] public void PerRegionSolidFillPreservesTheRibbonAndClipsPlantsToTheFrame() {
            var layer=new FloralLayer();var settings=layer.Defaults();settings.Values["plantingSystem"]="props";settings.Values["shape"]="collage";
            settings.Values["motifs"]=new[]{new FloralMotif{Kind="ring",Id="circle",Size=50,Width=4,Flower="Ribbon"}.Encode()};
            settings.Values["fills"]=new[]{new FloralFill{X=0,Z=0,Style="single",Flower="Inside"}.Encode(),new FloralFill{X=-40,Z=-40,Style="empty"}.Encode()};
            var input=new AreaInput{Shape=new AreaShape{Ring=Frame()},Settings=settings,Site=new SiteContext{HeightAt=p=>0},Seed=42};
            var plan=layer.Plan(input,CancellationToken.None);var origin=new float2(50,50);
            Assert.Contains(plan.Objects,o=>o.Asset.Id=="Inside");Assert.Contains(plan.Objects,o=>o.Asset.Id=="Ribbon");
            Assert.All(plan.Objects,o=>Assert.True(Poly.Contains(Poly.To2D(Frame()),o.Position.xz)));
            Assert.All(plan.Objects.Where(o=>o.Asset.Id=="Inside"),o=>Assert.True(math.distance(o.Position.xz,origin)<25));
        }
        [Fact] public void AutomaticCurvesUseAnIndependentPaletteAndRespectPerCurveOverrides() {
            var layer = new FloralLayer(); var settings = layer.Defaults(); settings.Values["plantingSystem"]="props";
            settings.Values["shape"] = "collage"; settings.Values["fillGaps"] = true;
            settings.Values["curvePalette"] = new[] { "WhiteRibbon" };
            settings.Values["flowerPalette"] = new[] { "RedFill" };
            settings.Values["motifs"] = new[] {
                new FloralMotif { Id="auto", Kind="ring", Size=65, Width=7 }.Encode(),
                new FloralMotif { Id="override", Kind="ring", Size=25, Width=7, Flower="BlueRibbon" }.Encode()
            };
            AreaPlan Plan() => layer.Plan(new AreaInput { Shape=new AreaShape { Ring=Frame() },
                Settings=settings, Site=new SiteContext { HeightAt=p=>0 }, Seed=42 }, CancellationToken.None);
            var before = Plan();
            Assert.Contains(before.Objects, o=>o.Asset.Id=="WhiteRibbon");
            Assert.Contains(before.Objects, o=>o.Asset.Id=="BlueRibbon");
            Assert.Contains(before.Objects, o=>o.Asset.Id=="RedFill");
            settings.Values["flowerPalette"] = new[] { "YellowFill", "PinkFill" };
            var after = Plan();
            Assert.Equal(before.Objects.Where(o=>o.Control=="motifs").Select(o=>(o.Position,o.Asset.Id,o.Variation)),
                after.Objects.Where(o=>o.Control=="motifs").Select(o=>(o.Position,o.Asset.Id,o.Variation)));
            Assert.DoesNotContain(after.Objects, o=>o.Asset.Id=="RedFill");
        }
    }
}
