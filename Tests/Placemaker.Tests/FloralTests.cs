using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FlowerPainter.Core;
using FlowerPainter.Core.Build;
using FlowerPainter.Core.Geometry;
using FlowerPainter.Core.Layers.Common;
using FlowerPainter.Core.Layers.Types.Floral;
using FlowerPainter.Core.Tool;
using Unity.Mathematics;
using Xunit;

namespace FlowerPainter.Tests
{
    [Collection(TimingCollection.Name)]
    public class FloralTests
    {
        private static float3[] Ring(float size = 70) => new[] { new float3(0,0,0), new float3(size,0,0), new float3(size,0,size), new float3(0,0,size) };
        private static AreaInput Input(string shape = "ring", float size = 70)
        {
            var settings = new FloralLayer().Defaults();
            settings.Values["shape"] = shape;
            return new AreaInput { Shape = new AreaShape { Ring = Ring(size), Source = AreaSource.Drawn }, Settings = settings,
                Site = new SiteContext { HeightAt = p => p.x * 0.03f + p.y * 0.01f }, Seed = 82 };
        }
        private static EdgeMarker Point(float x, float z) => new EdgeMarker { Kind = MarkerKinds.FloralPoint, RingSegment = -1, Position = new float3(x,0,z) };

        [Theory]
        [InlineData("ring")]
        [InlineData("circle")]
        [InlineData("rosette")]
        [InlineData("spiral")]
        [InlineData("heart")]
        [InlineData("outline")]
        [InlineData("freeform")]
        [InlineData("flow")]
        [InlineData("islands")]
        public void EveryShapeBuildsFlowersInsideTheSelectionWithoutChangingTerrain(string shape)
        {
            var input = Input(shape);
            var layer = new FloralLayer();
            var plan = layer.Plan(input, CancellationToken.None);
            Assert.False(plan.HasErrors);
            Assert.True(plan.Objects.Count >= 10);
            Assert.All(plan.Objects, o =>
            {
                Assert.Equal(AssetCategories.Flower, o.Category);
                Assert.True(Poly.Contains(input.Shape.Ring2D(), o.Position.xz));
                Assert.InRange(math.abs(o.Position.y - input.Site.HeightAt(o.Position.xz)), 0, 0.001f);
            });
            Assert.Empty(plan.Nets); Assert.Empty(plan.Surfaces); Assert.Empty(plan.Areas);
            Assert.Empty(plan.Paint); Assert.Empty(plan.Terraform); Assert.Empty(plan.Zoning);
            var again = layer.Plan(input, CancellationToken.None);
            Assert.Equal(plan.Objects.Select(o => (o.Position, o.Asset.Id, o.Variation)), again.Objects.Select(o => (o.Position, o.Asset.Id, o.Variation)));
        }

        [Fact]
        public void RingHasAnOpenCenterAndDiscFillsIt()
        {
            var layer = new FloralLayer();
            var ring = layer.Plan(Input("ring"), CancellationToken.None);
            var disc = layer.Plan(Input("circle"), CancellationToken.None);
            var center = new float2(35,35);
            Assert.DoesNotContain(ring.Objects, o => math.distance(o.Position.xz, center) < 12);
            Assert.Contains(disc.Objects, o => math.distance(o.Position.xz, center) < 2);
            Assert.Contains(disc.Objects, o => math.distance(o.Position.xz, center) > 15);
        }

        [Fact]
        public void OptionalShrubsRemainASeparateBorder()
        {
            var input = Input();
            input.Settings.Values["shrubs"] = true;
            var plan = new FloralLayer().Plan(input, CancellationToken.None);
            Assert.Contains(plan.Objects, o => o.Role == ObjectRole.Bush && o.Category == AssetCategories.Bush);
            Assert.Contains(plan.Objects, o => o.Category == AssetCategories.Flower);
            Assert.All(plan.Objects.Where(o => o.Role == ObjectRole.Bush), o => Assert.DoesNotContain("Flower", o.Asset.Id));
        }

        [Fact]
        public void CustomCurveUsesInteriorPointsAndKeepsAwayFromReservedLand()
        {
            var input = Input("freeform");
            input.Markers.AddRange(new[] { Point(10,20), Point(30,40), Point(55,20) });
            input.Reserved.AddDisc(new float2(30,40), 5);
            var plan = new FloralLayer().Plan(input, CancellationToken.None);
            Assert.NotEmpty(plan.Objects);
            Assert.Contains(plan.Objects, o => math.distance(o.Position.xz, new float2(10,20)) < 3);
            Assert.Contains(plan.Objects, o => math.distance(o.Position.xz, new float2(55,20)) < 3);
            Assert.DoesNotContain(plan.Objects, o => math.distance(o.Position.xz, new float2(30,40)) < 5);
        }

        [Fact]
        public void ComposerPreservesInteriorPointsAndDoesNotGradeFlowersOnASlope()
        {
            var input = Input("freeform");
            var recipe = Recipe.Single(new FloralLayer(), 82);
            recipe.Root.Settings = input.Settings;
            recipe.Root.Markers.AddRange(new[] { Point(10,20), Point(35,45), Point(60,20) });
            var plan = Composer.Compose(recipe, input.Shape, input.Site, CancellationToken.None);
            Assert.False(plan.HasErrors);
            Assert.NotEmpty(plan.Layers[0].Objects);
            Assert.Empty(plan.Layers[0].Terraform);
            Assert.All(plan.Layers[0].Objects, o => Assert.InRange(math.abs(o.Position.y - input.Site.HeightAt(o.Position.xz)), 0, 0.001f));
            var projected = Composer.MarkersOn(recipe.Root.Markers, Ring());
            Assert.Equal(3, projected.Count);
            Assert.Equal(recipe.Root.Markers[1].Position, projected[1].Position);
        }

        [Fact]
        public void MovingTheOutlineDoesNotSnapCurvePointsToItsEdgeOrDropThemFarFromRoads()
        {
            var recipe = Recipe.Single(new FloralLayer(), 82);
            var point = Point(30,30);
            recipe.Root.Markers.Add(point);
            var old = Ring(); var changed = Ring(80);
            MarkerFollow.Reproject(recipe, old, changed, null, () => new[] { new NearbyNet { A = new float3(-100,0,-100), D = new float3(-90,0,-100), Width = 8 } });
            Assert.Single(recipe.Root.Markers);
            Assert.Equal(point.Position, recipe.Root.Markers[0].Position);
            Assert.Equal(-1, recipe.Root.Markers[0].RingSegment);
        }

        [Fact]
        public void LargeFlowerDiscKeepsTheRequestedDensity()
        {
            var input = Input("circle", 100);
            input.Settings.Values["spacing"] = 0.75;
            input.Settings.Values["shrubs"] = true;
            var plan = new FloralLayer().Plan(input, CancellationToken.None);
            Assert.True(plan.Objects.Count >= 100);
            Assert.True(plan.Objects.Count > 3200);
            Assert.DoesNotContain(plan.Issues, i => i.Key == "FlowerPainter.Floral.Issue.DensityLimited");
            Assert.Contains(plan.Objects, o => math.distance(o.Position.xz, new float2(50,50)) < 10);
        }

        [Fact]
        public void ExplicitFlowerMaterialsControlTheThreeBands()
        {
            var input = Input();
            input.Settings.Values["flowerAssets"] = new[] { "Flowerbed01" };
            input.Settings.Values["accentAssets"] = new[] { "Flowerbed02" };
            input.Settings.Values["detailAssets"] = new[] { "FlowerBushWild01" };
            var plan = new FloralLayer().Plan(input, CancellationToken.None);
            Assert.Contains(plan.Objects, o => o.Asset.Id == "Flowerbed01");
            Assert.Contains(plan.Objects, o => o.Asset.Id == "Flowerbed02");
            Assert.Contains(plan.Objects, o => o.Asset.Id == "FlowerBushWild01");
        }

        [Theory]
        [InlineData("Flowerbed01", true)]
        [InlineData("FlowerBushWild01", true)]
        [InlineData("VFP BluebellWild", true)]
        [InlineData("VFP PoppyBushWild", true)]
        [InlineData("VFP LilacWild", true)]
        [InlineData("GreenBushWild01", false)]
        [InlineData("FlowerPot01", true)]
        [InlineData("JaysFlowerPlanter01", true)]
        [InlineData("FlowerBushWildDead02", false)]
        [InlineData("FlowerBushCity01", true)]
        [InlineData("VFP VibrantBushCity", true)]
        [InlineData("DahliaCluster", true)]
        [InlineData("BillboardLarge-FlowersFlours", false)]
        [InlineData("AStand01-FlowersFlours", false)]
        [InlineData("GasStationPylon-FlowersFlours", false)]
        public void FlowerPickerIncludesLoadedFlowerKindsAndExcludesGreenBushes(string name, bool expected)
        {
            var categories = AssetHeuristics.Classify(name, AssetTraits.StaticObject | AssetTraits.Plant | AssetTraits.HasMesh);
            Assert.Equal(expected, categories.Contains(AssetCategories.Flower));
        }

        [Fact]
        public void ManualFlowerPotsAreAvailableWithoutBecomingAutomaticGroundCover()
        {
            var rules = new FloralCategoryRules();
            Assert.False(rules.AutoEligible(AssetCategories.Flower, "flowerpot01", AssetTraits.StaticObject | AssetTraits.HasMesh));
            var categories = new List<string>();
            rules.Classify("flowershop", AssetTraits.Building | AssetTraits.HasMesh, categories);
            Assert.DoesNotContain(AssetCategories.Flower, categories);
            rules.Classify("vfp bluebellwild", AssetTraits.StaticObject | AssetTraits.Plant | AssetTraits.Tree | AssetTraits.HasMesh, categories);
            Assert.Contains(AssetCategories.Flower, categories);
        }

        [Fact]
        public void PlanningCanBeCancelled()
        {
            using var source = new CancellationTokenSource(); source.Cancel();
            Assert.Throws<OperationCanceledException>(() => new FloralLayer().Plan(Input(), source.Token));
        }
    }
}
