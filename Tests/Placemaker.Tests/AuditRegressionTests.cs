using System.Collections.Generic;
using FlowerPainter.Core;
using FlowerPainter.Core.Build;
using FlowerPainter.Core.Geometry;
using FlowerPainter.Core.Tool;
using Unity.Mathematics;
using Xunit;

namespace FlowerPainter.Tests
{
    public class AuditRegressionTests
    {
        private static float3[] Square(float offset = 0) => new[] { new float3(offset, 0, 0), new float3(offset + 50, 0, 0), new float3(offset + 50, 0, 50), new float3(offset, 0, 50) };
        private static ComposedPlan Terrain(float offset = 0, float height = -2, float water = float.NaN)
        {
            var p = new ComposedPlan();
            var l = new AreaPlan();
            l.Terraform.Add(new PlannedTerraform { Ring = Square(offset), TargetHeight = height, WaterLevel = water, Permanent = true });
            p.Layers.Add(l); return p;
        }

        [Fact]
        public void BrushClippingKeepsGlobalAndUnlocatedErrorsAndOnlyRelevantLocatedIssues()
        {
            var source = new ComposedPlan();
            source.Issues.Add(new PlanIssue { Severity = IssueSeverity.Error, Key = "global" });
            var layer = new AreaPlan(); source.Layers.Add(layer);
            layer.Issues.Add(new PlanIssue { Severity = IssueSeverity.Error, Key = "inside", At = new float3(20, 0, 20) });
            layer.Issues.Add(new PlanIssue { Severity = IssueSeverity.Error, Key = "outside", At = new float3(100, 0, 100) });
            layer.Objects.Add(new PlannedObject { Position = new float3(20, 0, 20), Asset = new AssetRef("flower") });
            var result = BrushAreaEdit.ClipPlan(source, new List<Region> { new Region(Poly.To2D(Square())) }, _ => 0);
            Assert.True(result.HasErrors);
            Assert.Single(result.Layers[0].Objects);
            Assert.Equal("global", Assert.Single(result.Issues).Key);
            Assert.Equal("inside", Assert.Single(result.Layers[0].Issues).Key);
            Assert.Equal(2, source.Layers[0].Issues.Count);
        }

        [Fact]
        public void ClippedAwayLocalErrorDoesNotBlockAnUnrelatedExtension()
        {
            var source = new ComposedPlan(); var l = new AreaPlan(); source.Layers.Add(l);
            l.Issues.Add(new PlanIssue { Severity = IssueSeverity.Error, At = new float3(200, 0, 200) });
            var result = BrushAreaEdit.ClipPlan(source, new List<Region> { new Region(Poly.To2D(Square())) }, _ => 0);
            Assert.False(result.HasErrors);
        }

        [Theory]
        [InlineData(100, -2, float.NaN)]
        [InlineData(0, -20, float.NaN)]
        [InlineData(0, -2, 8)]
        public void PermanentConsentChangesForPositionDepthAndWater(float offset, float height, float water)
        {
            Assert.NotEqual(UiModels.PermanentFingerprint(Terrain()), UiModels.PermanentFingerprint(Terrain(offset, height, water)));
        }

        [Fact]
        public void PermanentConsentIgnoresDecorationButDetectsSlopedVertices()
        {
            var plan = Terrain(); var original = UiModels.PermanentFingerprint(plan);
            plan.Layers[0].Objects.Add(new PlannedObject { Asset = new AssetRef("another-flower") });
            Assert.Equal(original, UiModels.PermanentFingerprint(plan));
            plan.Layers[0].Terraform[0].Sloped = true;
            plan.Layers[0].Terraform[0].Ring[0].y += 2;
            Assert.NotEqual(original, UiModels.PermanentFingerprint(plan));
        }

        [Fact]
        public void NoPermanentTerrainNeedsNoConsent()
        {
            Assert.Equal("", UiModels.PermanentFingerprint(new ComposedPlan()));
            var p = Terrain(); p.Layers[0].Terraform[0].Permanent = false;
            Assert.Equal("", UiModels.PermanentFingerprint(p));
        }

        [Fact]
        public void EmptyFlowerCompositionAsksForAPatternInsteadOfAnEntrance()
        {
            var gen = new FlowerPainter.Core.Layers.Types.Floral.FloralLayer();
            var recipe = new Recipe { Root = new RecipeStep { Layer = "floral", Settings = gen.Defaults() } };
            var editor = new DesignEditor(id => id == "floral" ? gen : null);
            var unready = editor.FirstUnready(recipe, new AreaShape { Ring = Square() }, new SiteContext());
            Assert.NotNull(unready);
            Assert.Equal("", unready.MarkerKind);
            Assert.Equal("FlowerPainter.Floral.Issue.AddMotif", unready.ReasonKey);
            Assert.Equal(0, MarkerKinds.Required(gen, gen.Defaults()));
        }

        [Fact]
        public void AnEmptyPlanCannotOfferBuildButTerrainOnlyPlansRemainBuildable()
        {
            var empty = new ComposedPlan(); empty.Layers.Add(new AreaPlan());
            Assert.False(empty.HasBuildableContent);
            Assert.True(Terrain().HasBuildableContent);
            var action = StageMachine.Primary(ToolStage.Review, new StageFacts { AreaValid = true, HasRecipe = true, PlanReady = true, PlanHasContent = false });
            Assert.False(action.Enabled);
            Assert.Equal("FlowerPainter.Build.NothingToBuild", action.Reason.Key);
        }
    }
}
