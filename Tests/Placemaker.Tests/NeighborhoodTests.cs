using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FlowerPainter.Core;
using FlowerPainter.Core.Build;
using FlowerPainter.Core.Geometry;
using FlowerPainter.Core.Layers.Common;
using FlowerPainter.Core.Layers.Roads;
using FlowerPainter.Core.Tool;
using Unity.Mathematics;
using Xunit;

namespace FlowerPainter.Tests
{
    [Collection(TimingCollection.Name)]
    public class NeighborhoodTests
    {
        private static float2[] Ring(float width, float height) => new[] { new float2(0, 0), new float2(width, 0), new float2(width, height), new float2(0, height) };

        private static List<RoadBlocks.Line> Streets(float w, float h) => new List<RoadBlocks.Line>
        {
            new RoadBlocks.Line { Points = new List<float2> { new float2(-8, -8), new float2(w+8, -8) }, HalfWidth = 8 },
            new RoadBlocks.Line { Points = new List<float2> { new float2(w+8, -8), new float2(w+8, h+8) }, HalfWidth = 8 },
            new RoadBlocks.Line { Points = new List<float2> { new float2(w+8, h+8), new float2(-8, h+8) }, HalfWidth = 8 },
            new RoadBlocks.Line { Points = new List<float2> { new float2(-8, h+8), new float2(-8, -8) }, HalfWidth = 8 },
        };

        private static AreaInput GardenInput(float w = 120, float h = 100)
        {
            var streets = Streets(w, h);
            var ring = NeighborhoodLayout.Gardens(Ring(w, h), 3, streets, CancellationToken.None).Single();
            var reserved = new ReservedSpace();
            foreach (var road in streets) reserved.AddCorridor(road.Points[0], road.Points[1], road.HalfWidth, NetRole.Street.ToString());
            return new AreaInput { Shape = new AreaShape { Ring = Poly.To3D(ring, _ => 0f) }, Reserved = reserved,
                RegionTags = new[] { NeighborhoodLayout.Target }, Site = new SiteContext { LayoutScale = 0.5f }, Seed = 42 };
        }

        [Fact]
        public void GardenReservesFrontageAndHasTwoStreetAccesses()
        {
            var input = GardenInput();
            var ring = input.Shape.Ring2D();
            Assert.True(Poly.Contains(ring, new float2(60, 50)));
            Assert.False(Poly.Contains(ring, new float2(12, 12)));
            var gates = NeighborhoodGardenLayer.Gates(input, LayerRegion.From(ring));
            Assert.Equal(2, gates.Count);
            Assert.True(math.distance(gates[0].Position.xz, gates[1].Position.xz) >= 20f);
            foreach (var p in ring) Assert.True(p.x >= -0.05f && p.y >= -0.05f && p.x <= 120.05f && p.y <= 100.05f);
        }

        [Fact]
        public void SmallBlocksKeepTheirHousingInsteadOfForcingAGarden()
        {
            Assert.Empty(NeighborhoodLayout.Gardens(Ring(45, 40), 3, Streets(45, 40), CancellationToken.None));
        }

        [Fact]
        public void DeeperHousingLeavesLessGarden()
        {
            var roads = Streets(180, 160);
            var shallow = NeighborhoodLayout.Gardens(Ring(180, 160), 2, roads, CancellationToken.None).Sum(r => Poly.Area(r));
            var deep = NeighborhoodLayout.Gardens(Ring(180, 160), 6, roads, CancellationToken.None).Sum(r => Poly.Area(r));
            Assert.True(shallow > deep && deep > 0);
        }

        [Fact]
        public void ConcaveBlockKeepsItsConnectedGardenEvenWhenItsCentroidIsOutside()
        {
            var block = new[] { new float2(0, 0), new float2(300, 0), new float2(300, 100),
                new float2(100, 100), new float2(100, 300), new float2(0, 300) };
            var streets = new List<RoadBlocks.Line>();
            for (int i = 0; i < block.Length; i++)
            {
                var a = block[i]; var b = block[(i + 1) % block.Length];
                var direction = math.normalizesafe(b - a);
                var outward = new float2(direction.y, -direction.x) * 8f;
                streets.Add(new RoadBlocks.Line { Points = new List<float2> { a + outward, b + outward }, HalfWidth = 8f });
            }
            var inner = LayerRegion.From(Poly.Offset(block, 26f).Single());
            Assert.False(Poly.Contains(inner.Ring, inner.Centroid));
            var garden = NeighborhoodLayout.Gardens(block, 3, streets, CancellationToken.None).Single();
            Assert.True(Poly.Contains(garden, inner.DeepestPoint(out _)));
            Assert.True(Poly.Area(garden) >= inner.Area);
            Assert.All(garden, p => Assert.True(p.x <= 100.05f || p.y <= 100.05f));
        }

        [Fact]
        public void ParkAdapterProducesWalksAndFurnitureWithoutManualMarkers()
        {
            var plan = new NeighborhoodGardenLayer().Plan(GardenInput(), CancellationToken.None);
            Assert.False(plan.HasErrors);
            Assert.NotEmpty(plan.Nets);
            Assert.Contains(plan.Objects, o => o.Role == ObjectRole.Tree);
            Assert.Contains(plan.Objects, o => o.Role == ObjectRole.Bench);
            Assert.Empty(plan.Zoning);
            Assert.Empty(plan.Terraform);
            Assert.Empty(plan.Paint);
        }

        private static Recipe Neighborhood(string zone = ZoneKinds.ResidentialLow, bool feature = true)
        {
            var recipe = Recipe.Single(new RoadsLayer(), 73);
            recipe.Root.Settings.Values[RoadsIds.GridSpacing] = 140.0;
            recipe.Root.Settings.Values[RoadsIds.Walkways] = 0.0;
            recipe.Root.Settings.Values[RoadsIds.ConnectExisting] = false;
            recipe.Root.Settings.Values[RoadsIds.PreZone] = zone;
            recipe.Root.Settings.Values[RoadsIds.HomeDepth] = 3.0;
            if (feature) recipe.Root.Children.Add(new RecipeStep { Layer = NeighborhoodGardenLayer.LayerId, Target = NeighborhoodLayout.Target, Seed = 91 });
            return recipe;
        }

        private static ComposedPlan Compose(Recipe recipe) => Composer.Compose(recipe,
            new AreaShape { Source = AreaSource.Drawn, Ring = Poly.To3D(Ring(420, 360), _ => 0f) },
            new SiteContext { LayoutScale = 0.5f }, CancellationToken.None);

        [Theory]
        [InlineData("green-neighborhood", "pocket", ZoneKinds.ResidentialLow)]
        [InlineData("natural-neighborhood", "natural", ZoneKinds.ResidentialRow)]
        [InlineData("formal-neighborhood", "formal", ZoneKinds.ResidentialMedium)]
        public void PresetsEnableGardensAndResidentialZoningTogether(string preset, string style, string zone)
        {
            var editor = new DesignEditor(GeneratorRegistry.Get);
            var recipe = editor.SingleLayerRecipe(new RoadsLayer(), 73);
            Assert.True(editor.ApplyPreset(recipe, "0", preset));
            var child = recipe.Root.Children.Single(c => c.Layer == NeighborhoodGardenLayer.LayerId);
            Assert.True(child.Enabled);
            Assert.Equal(style, child.Settings.S("style", ""));
            Assert.Equal(zone, recipe.Root.Settings.S(RoadsIds.PreZone, ""));
            Assert.Contains(Compose(recipe).Layers, l => l.GeneratorId == NeighborhoodGardenLayer.LayerId);
        }

        [Fact]
        public void ResidentialModeZonesFrontageAndProtectsOnlyGardenFootprints()
        {
            var plan = Compose(Neighborhood());
            Assert.False(plan.HasErrors);
            Assert.Contains(plan.Layers, l => l.GeneratorId == NeighborhoodGardenLayer.LayerId);
            Assert.NotEmpty(plan.Footprints);
            var roads = plan.Layers.First(l => l.GeneratorId == "roads");
            Assert.Equal(3, roads.Facts[RoadPreZone.FactDepth]);
            Assert.NotEmpty(roads.Zoning);
            Assert.True(plan.Footprints.Sum(p => Poly.Area(p)) < 420*360*0.6f);
            foreach (var cell in plan.ZoneCells.Where(c => c.State == ZoneCellState.Zoned))
                Assert.DoesNotContain(plan.Footprints, p => Poly.Contains(p, (cell.A + cell.B + cell.C + cell.D) * 0.25f));
        }

        [Fact]
        public void FeatureOffKeepsOriginalRoadsAndFullDepthZoning()
        {
            var plan = Compose(Neighborhood(feature: false));
            Assert.Single(plan.Layers);
            Assert.Empty(plan.Footprints);
            Assert.False(plan.Layers[0].Facts.ContainsKey(RoadPreZone.FactDepth));
            Assert.NotEmpty(plan.Layers[0].Zoning);
        }

        [Theory]
        [InlineData(ZoneKinds.CommercialLow)]
        [InlineData(ZoneKinds.Industrial)]
        [InlineData(RoadPreZone.None)]
        public void OtherZonesAreNotSilentlyConvertedToResidentialGardens(string zone)
        {
            var plan = Compose(Neighborhood(zone));
            Assert.DoesNotContain(plan.Layers, l => l.GeneratorId == NeighborhoodGardenLayer.LayerId);
            Assert.Contains(plan.AllIssues(), i => i.Key == "FlowerPainter.Neighborhood.Issue.ResidentialOnly");
        }
    }
}
