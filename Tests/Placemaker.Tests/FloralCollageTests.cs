using System;
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
    public class FloralCollageTests
    {
        private static AreaInput Input(params FloralMotif[] motifs)
        {
            var s = new FloralLayer().Defaults(); s.Values["shape"]="collage";
            s.Values["motifs"] = motifs.Select(m => m.Encode()).ToArray();
            s.Values["flowerPalette"] = new[] { "GapFlowerA", "GapFlowerB", "GapFlowerC" };
            return new AreaInput { Settings = s, Seed = 455,
                Shape = new AreaShape { Ring = new[] { new float3(0,0,0), new float3(60,0,0), new float3(60,0,50), new float3(0,0,50) } },
                Site = new SiteContext { HeightAt = p => p.x * 0.03f } };
        }
        private static FloralMotif Ring() => new FloralMotif { Id = "ring", Kind = "ring", X = -38, Z = 2, Size = 76, Width = 5, Flower = "RingFlowers" };
        private static FloralMotif Wave() => new FloralMotif { Id = "wave", Kind = "wave", X = 4, Z = -3, Size = 72, Width = 6, Angle = -22, Flower = "WaveFlowers" };

        [Fact]
        public void AnOutsideCircleProducesOnlyItsIntersectingArcWithoutFittingItIntoTheFrame()
        {
            var ring = Ring(); var input = Input(ring); var region = LayerRegion.From(input.Shape.Ring); var origin = FloralMotif.Origin(region);
            Assert.False(region.Contains(ring.Center(origin)));
            var plan = new FloralLayer().Plan(input, CancellationToken.None);
            Assert.InRange(plan.Objects.Count, 50, FloralCarpet.MaxObjects);
            Assert.All(plan.Objects, o => { Assert.True(region.Contains(o.Position.xz)); Assert.True(ring.Covers(o.Position.xz, origin)); Assert.Equal("RingFlowers", o.Asset.Id); });
            Assert.Equal(ring.Encode(), input.Settings.L("motifs")[0]);
            Assert.DoesNotContain(plan.Objects, o => math.distance(o.Position.xz, origin) < 4 && !ring.Covers(o.Position.xz, origin));
            Assert.Equal(0, plan.Facts["floral.gapFlowers"]);
        }

        [Fact]
        public void TheLastOverlappingPatternWinsAndFlowersAreNotStacked()
        {
            var a = Ring(); var b = Wave(); var input = Input(a, b); var origin = FloralMotif.Origin(LayerRegion.From(input.Shape.Ring));
            var plan = new FloralLayer().Plan(input, CancellationToken.None);
            Assert.Contains(plan.Objects, o => o.Asset.Id == a.Flower); Assert.Contains(plan.Objects, o => o.Asset.Id == b.Flower);
            Assert.All(plan.Objects, o => Assert.Equal(b.Covers(o.Position.xz, origin) ? b.Flower : a.Flower, o.Asset.Id));
            Assert.Equal(plan.Objects.Count, plan.Objects.Select(o => o.Position).Distinct().Count());
            Assert.DoesNotContain(plan.Objects, o => !a.Covers(o.Position.xz, origin) && !b.Covers(o.Position.xz, origin));
        }

        [Theory]
        [InlineData("flow")]
        [InlineData("islands")]
        public void FillAndRerollTouchOnlyTheGapsAndKeepThePatternsInPlace(string style)
        {
            var input = Input(Ring(), Wave()); var layer = new FloralLayer();
            var before = layer.Plan(input, CancellationToken.None);
            input.Settings.Values["gapStyle"] = style; input.Settings.Values["fillGaps"] = true;
            var filled = layer.Plan(input, CancellationToken.None);
            Assert.True(filled.Objects.Count > before.Objects.Count);
            Assert.Equal(before.Objects.Select(o => (o.Position, o.Asset.Id, o.Yaw, o.Variation)), filled.Objects.Where(o => o.Control == "motifs").Select(o => (o.Position, o.Asset.Id, o.Yaw, o.Variation)));
            input.Seed = 9823;
            var rerolled = layer.Plan(input, CancellationToken.None);
            Assert.Equal(filled.Objects.Where(o => o.Control == "motifs").Select(o => (o.Position, o.Asset.Id, o.Yaw, o.Variation)), rerolled.Objects.Where(o => o.Control == "motifs").Select(o => (o.Position, o.Asset.Id, o.Yaw, o.Variation)));
            Assert.NotEqual(filled.Objects.Where(o => o.Control != "motifs").Select(o => o.Asset.Id).ToArray(), rerolled.Objects.Where(o => o.Control != "motifs").Select(o => o.Asset.Id).ToArray());
            Assert.Empty(filled.Terraform); Assert.Empty(filled.Surfaces); Assert.Empty(filled.Nets);
            Assert.All(filled.Objects, o => Assert.InRange(math.abs(o.Position.y - input.Site.HeightAt(o.Position.xz)), 0, 0.001f));
        }

        [Fact]
        public void EmptyFrameWaitsForPatternsAndCanAlsoBeFilledAutomatically()
        {
            var layer = new FloralLayer(); var input = Input();
            Assert.False(layer.CheckReady(input).Ready); Assert.Empty(layer.Plan(input, CancellationToken.None).Objects);
            input.Settings.Values["fillGaps"] = true;
            Assert.True(layer.CheckReady(input).Ready); Assert.NotEmpty(layer.Plan(input, CancellationToken.None).Objects);
        }

        [Fact]
        public void EditingOnePatternPreservesTheOtherPatternAndTheSourceSettings()
        {
            var a = Ring(); var b = Wave(); var input = Input(a,b); var old = input.Settings.L("motifs");
            var editor = new DesignEditor(GeneratorRegistry.Get); var recipe = Recipe.Single(new FloralLayer(), 455); recipe.Root.Settings = input.Settings.Clone();
            a.X += 15; a.Z -= 6; a.Size *= 1.3f; a.Angle += 30;
            Assert.True(editor.SetValue(recipe, "0", "motifs", new[] { a.Encode(), b.Encode() }, true));
            Assert.Equal(b.Encode(), recipe.Root.Settings.L("motifs")[1]);
            Assert.NotEqual(old[0], recipe.Root.Settings.L("motifs")[0]);
            Assert.Equal(old, input.Settings.L("motifs"));
        }

        [Fact]
        public void PatternCodecPreservesPlantNamesAndRejectsMalformedData()
        {
            var m = Ring(); m.Flower = "Flower | 花卉 / red";
            Assert.Equal(m.Encode(), FloralMotif.Parse(m.Encode()).Encode());
            Assert.Null(FloralMotif.Parse("v1|a|ring|NaN|0|40|0|5|Flower"));
            Assert.Null(FloralMotif.Parse("broken"));
        }

        [Theory]
        [InlineData(1)] [InlineData(2)] [InlineData(3)]
        public void ExampleCompositionsBuildAsClippedPatternsWithColouredGaps(int example)
        {
            var input = Input(); input.Settings.Values["motifs"] = FloralMotif.Example(example); input.Settings.Values["fillGaps"] = true;
            var plan = new FloralLayer().Plan(input, CancellationToken.None);
            Assert.False(plan.HasErrors); Assert.Contains(plan.Objects, o => o.Control == "motifs"); Assert.Contains(plan.Objects, o => o.Control != "motifs");
            Assert.InRange(plan.Objects.Count, 100, FloralCarpet.MaxObjects);
        }
    }
}
