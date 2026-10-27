using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FlowerPainter.Core;
using FlowerPainter.Core.Geometry;
using FlowerPainter.Core.Layers.Common;
using FlowerPainter.Core.Layers.Types.Floral;
using FlowerPainter.Core.Tool;
using Unity.Mathematics;
using Xunit;

namespace FlowerPainter.Tests
{
    public class FloralCarpetTests
    {
        private static float3[] Outline(string shape)
        {
            float2[] points = shape == "l"
                ? new[] { new float2(0,0), new float2(70,0), new float2(70,25), new float2(25,25), new float2(25,65), new float2(0,65) }
                : shape == "wedge"
                ? new[] { new float2(0,0), new float2(72,8), new float2(48,64), new float2(6,45) }
                : new[] { new float2(0,0), new float2(30,8), new float2(65,0), new float2(65,45), new float2(30,32), new float2(0,45) };
            return Poly.To3D(points, p => 0);
        }

        private static AreaInput Input(string mode, string shape = "l")
        {
            var settings = new FloralLayer().Defaults(); settings.Values["plantingSystem"]="props";
            settings.Values["shape"] = mode;
            settings.Values["bandWidth"] = 6.0;
            return new AreaInput { Shape = new AreaShape { Ring = Outline(shape), Source = AreaSource.Drawn }, Settings = settings,
                Site = new SiteContext { HeightAt = p => p.x * 0.04f }, Seed = 123 };
        }

        [Theory]
        [InlineData("flow", "l")]
        [InlineData("flow", "wedge")]
        [InlineData("flow", "pinch")]
        [InlineData("islands", "l")]
        [InlineData("islands", "wedge")]
        [InlineData("islands", "pinch")]
        public void CarpetsCoverEveryInteriorPartOfConcaveAndIrregularPlots(string mode, string shape)
        {
            var input = Input(mode, shape);
            var plan = new FloralLayer().Plan(input, CancellationToken.None);
            var region = LayerRegion.From(input.Shape.Ring);
            Assert.False(plan.HasErrors);
            Assert.True(plan.Objects.Count >= 500);
            Assert.DoesNotContain(plan.Issues, i => i.Key == ObjectBudget.IssueKey);
            Assert.Equal(plan.Objects.Count, plan.Facts["floral.flowers"]);
            Assert.Equal(3, plan.Objects.Select(o => o.Control).Distinct().Count());
            Assert.All(plan.Objects, o =>
            {
                Assert.True(region.Contains(o.Position.xz));
                Assert.Equal(AssetCategories.Flower, o.Category);
                Assert.InRange(math.abs(o.Position.y - input.Site.HeightAt(o.Position.xz)), 0, 0.001f);
            });
            float pitch = (float)plan.Facts["floral.spacing"];
            for (float y = region.Min.y; y < region.Max.y; y += 2)
                for (float x = region.Min.x; x < region.Max.x; x += 2)
                {
                    var p = new float2(x, y);
                    if (!region.ContainsDisc(p, pitch * 2)) continue;
                    Assert.Contains(plan.Objects, o => math.distance(o.Position.xz, p) <= pitch);
                }
            Assert.Empty(plan.Nets); Assert.Empty(plan.Paint); Assert.Empty(plan.Surfaces); Assert.Empty(plan.Areas); Assert.Empty(plan.Terraform);
        }

        [Theory]
        [InlineData("flow")]
        [InlineData("islands")]
        public void RerollChangesColourCompositionWithoutChangingPlantsWidthDensityOrBoundary(string mode)
        {
            var layer = new FloralLayer();
            var editor = new DesignEditor(GeneratorRegistry.Get);
            var recipe = Recipe.Single(layer, 123);
            recipe.Root.Settings = Input(mode).Settings;
            editor.SetValue(recipe, "0", "flowerAssets", new[] { "Flowerbed01" }, true);
            editor.SetValue(recipe, "0", "bandWidth", 7.0, true);
            var beforeSettings = recipe.Root.Settings.Clone();
            var input = Input(mode);
            var before = Composer.Compose(recipe, input.Shape, input.Site, CancellationToken.None).Layers[0];
            Assert.True(editor.Randomize(recipe, "0", 98765));
            Assert.True(beforeSettings.ContentEquals(recipe.Root.Settings));
            var after = Composer.Compose(recipe, input.Shape, input.Site, CancellationToken.None).Layers[0];
            Assert.Equal(before.Objects.Select(o => o.Position), after.Objects.Select(o => o.Position));
            int changed = before.Objects.Zip(after.Objects, (a, b) => a.Control != b.Control ? 1 : 0).Sum();
            Assert.True(changed > before.Objects.Count / 10, "New seed should visibly change the colour regions.");
            var again = Composer.Compose(recipe, input.Shape, input.Site, CancellationToken.None).Layers[0];
            Assert.Equal(after.Objects.Select(o => (o.Position, o.Asset.Id, o.Scale)), again.Objects.Select(o => (o.Position, o.Asset.Id, o.Scale)));
        }

        [Theory]
        [InlineData("flow")]
        [InlineData("islands")]
        public void ShrubsReplaceOneColourRegionOnlyWhenEnabled(string mode)
        {
            var input = Input(mode);
            input.Settings.Values["shrubs"] = true;
            var plan = new FloralLayer().Plan(input, CancellationToken.None);
            Assert.Contains(plan.Objects, o => o.Role == ObjectRole.Bush);
            Assert.Contains(plan.Objects, o => o.Role == ObjectRole.Planter);
            Assert.All(plan.Objects.Where(o => o.Role == ObjectRole.Bush), o => Assert.Equal("shrubAssets", o.Control));
        }

        [Fact]
        public void LargeCarpetExceeds3200WithoutChangingSpacingOrScale()
        {
            var input = Input("flow");
            input.Shape.Ring = new[] { new float3(0,0,0), new float3(120,0,0), new float3(120,0,100), new float3(0,0,100) };
            var plan = new FloralLayer().Plan(input, CancellationToken.None);
            Assert.True(plan.Objects.Count > 5000);
            Assert.Equal(1.5, plan.Facts["floral.spacing"], 5);
            Assert.All(plan.Objects, o => Assert.Equal(0.65f, o.Scale));
            Assert.DoesNotContain(plan.Issues, i => i.Key == "FlowerPainter.Floral.Issue.CarpetBudget" || i.Key == ObjectBudget.IssueKey);
            Assert.Contains(plan.Objects, o => o.Position.x < 5 && o.Position.z < 5);
            Assert.Contains(plan.Objects, o => o.Position.x > 115 && o.Position.z > 95);
        }

        [Theory]
        [InlineData("flow")]
        [InlineData("islands")]
        [InlineData("collage")]
        public void OverlapIncreasesDensityWithoutShrinkingProps(string shape)
        {
            var input = NativeCarpet(); input.Settings.Values["shape"] = shape;
            input.Settings.Values["overlap"] = 0.0;
            var sparse = new FloralLayer().Plan(input, CancellationToken.None);
            input.Settings.Values["overlap"] = 50.0;
            var dense = new FloralLayer().Plan(input, CancellationToken.None);
            Assert.True(dense.Objects.Count > sparse.Objects.Count * 3.8);
            Assert.Equal(sparse.Facts["floral.spacing"] / 2, dense.Facts["floral.spacing"], 5);
            Assert.All(dense.Objects.Concat(sparse.Objects), o => Assert.Equal(0.65f, o.Scale));
            AssertInteriorCovered(input, dense);
            input.Settings.Values["flowerScale"] = 1.2;
            var larger = new FloralLayer().Plan(input, CancellationToken.None);
            Assert.Equal(dense.Facts["floral.spacing"], larger.Facts["floral.spacing"]);
            Assert.All(larger.Objects, o => Assert.Equal(1.2f, o.Scale));
        }

        [Theory]
        [InlineData("flow", 1)]
        [InlineData("flow", 2)]
        [InlineData("flow", 4)]
        [InlineData("flow", 7)]
        [InlineData("islands", 1)]
        [InlineData("islands", 2)]
        [InlineData("islands", 4)]
        [InlineData("islands", 7)]
        [InlineData("ring", 4)]
        public void PlayerPaletteUsesOnlySelectedPlantsAndSupportsMoreThanThreeColours(string shape, int count)
        {
            var input = Input(shape);
            var ids = Enumerable.Range(1, count).Select(n => "CustomFlower" + n).ToArray();
            input.Settings.Values["flowerPalette"] = ids;
            input.Shape.Ring = new[] { new float3(0,0,0), new float3(70,0,0), new float3(70,0,70), new float3(0,0,70) };
            var layer = new FloralLayer();
            var plan = layer.Plan(input, CancellationToken.None);
            Assert.NotEmpty(plan.Objects);
            Assert.All(plan.Objects, o => Assert.Contains(o.Asset.Id, ids));
            Assert.Equal(count, plan.Objects.Select(o => o.Asset.Id).Distinct().Count());
            var settings = layer.Randomize(input.Settings, 9876, new HashSet<string>());
            Assert.Equal(ids, settings.L("flowerPalette"));
        }

        [Fact]
        public void CarpetRespectsReservedSpaceAndCancellation()
        {
            var input = Input("flow");
            input.Reserved.AddDisc(new float2(15,15), 6);
            var plan = new FloralLayer().Plan(input, CancellationToken.None);
            Assert.DoesNotContain(plan.Objects, o => math.distance(o.Position.xz, new float2(15,15)) < 6);
            using var source = new CancellationTokenSource(); source.Cancel();
            Assert.Throws<OperationCanceledException>(() => new FloralLayer().Plan(input, source.Token));
        }

        // Native asset bounds measured from the player's catalog, including an elongated bed.
        private static AreaInput NativeCarpet(float spacing = 1.5f)
        {
            var input = Input("collage");
            input.Shape.Ring = new[] { new float3(0,0,0), new float3(60,0,0), new float3(60,0,50), new float3(0,0,50) };
            input.Settings.Values["fillGaps"] = true;
            input.Settings.Values["spacing"] = spacing;
            input.Settings.Values["flowerPalette"] = new[] { "Flowerbed01", "Flowerbed02", "FlowerBushWildChild02" };
            input.Site.Assets = new Dictionary<string, AssetInfo>
            {
                ["Flowerbed01"] = new AssetInfo { BoundsMin = new float3(-1.23f,0,-1.23f), BoundsMax = new float3(1.29f,1.11f,1.28f) },
                ["Flowerbed02"] = new AssetInfo { BoundsMin = new float3(-0.86f,0,-1.21f), BoundsMax = new float3(0.83f,1.06f,1.22f) },
                ["FlowerBushWildChild02"] = new AssetInfo { BoundsMin = new float3(-0.82f,0,-0.77f), BoundsMax = new float3(0.82f,0.87f,0.77f) }
            };
            return input;
        }

        private static void AssertInteriorCovered(AreaInput input, AreaPlan plan)
        {
            var region = LayerRegion.From(input.Shape.Ring);
            // Check actual short-axis footprints, not the planner's coverage estimate.
            var discs = plan.Objects.Select(o =>
            {
                var bounds = input.Site.Assets[o.Asset.Id];
                float radius = math.cmin(bounds.BoundsMax.xz - bounds.BoundsMin.xz) * 0.5f * o.Scale;
                return (position: o.Position.xz, radius);
            }).ToArray();
            int checkedPoints = 0;
            for (float z = region.Min.y; z < region.Max.y; z += 0.5f)
                for (float x = region.Min.x; x < region.Max.x; x += 0.5f)
                {
                    var p = new float2(x,z);
                    if (!region.ContainsDisc(p, (float)plan.Facts["floral.spacing"] * 2)) continue;
                    Assert.Contains(discs, d => math.distancesq(d.position, p) <= d.radius * d.radius);
                    checkedPoints++;
                }
            Assert.True(checkedPoints > 100);
        }

        [Fact]
        public void ExplicitOverlapCoversNativePropsWhileDefaultIsZero()
        {
            var input = NativeCarpet();
            Assert.Equal(0, input.Settings.F("overlap", -1));
            var sparse = new FloralLayer().Plan(input, CancellationToken.None);
            Assert.Equal(1.5, sparse.Facts["floral.spacing"], 5);
            input.Settings.Values["overlap"] = 50.0;
            var plan = new FloralLayer().Plan(input, CancellationToken.None);
            Assert.True(plan.Objects.Count > sparse.Objects.Count * 3.8);
            Assert.True(plan.Objects.Count > 5000);
            Assert.Equal(3, plan.Objects.Select(o => o.Asset.Id).Distinct().Count());
            AssertInteriorCovered(input, plan);
        }

        [Fact]
        public void WiderSpacingReducesPropsWithoutResizingThem()
        {
            var input = NativeCarpet();
            var dense = new FloralLayer().Plan(input, CancellationToken.None);
            input.Settings.Values["spacing"] = 2.0;
            var wide = new FloralLayer().Plan(input, CancellationToken.None);
            Assert.True(wide.Objects.Count < dense.Objects.Count * 0.7);
            Assert.All(wide.Objects.Concat(dense.Objects), o => Assert.Equal(0.65f, o.Scale));
        }
    }
}
