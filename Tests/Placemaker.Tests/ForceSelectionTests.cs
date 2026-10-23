using System;
using System.Linq;
using FlowerPainter.Core.Geometry;
using FlowerPainter.Core.Tool;
using Unity.Mathematics;
using Xunit;

namespace FlowerPainter.Tests
{
    public class ForceSelectionTests
    {
        private static readonly float2[] Ring = { new float2(-20,-20), new float2(20,-20), new float2(20,20), new float2(-20,20) };

        [Fact]
        public void ForceBypassesEveryStopForPointsBucketAndBrushWithoutChangingPreferences()
        {
            var defaults = BrushRules.Defaults();
            var forced = UiModels.ParseBrushRules("{\"forceSelection\":true}", defaults);
            foreach (BrushObstacleKind kind in Enum.GetValues(typeof(BrushObstacleKind)))
            {
                Assert.False(forced.Stops(kind));
                Assert.False(forced.ForOutline().Stops(kind));
            }
            var obstacles = new[] { new BrushObstacle(BrushObstacleKind.FlowerPainterBuild, Ring) };
            Assert.Null(defaults.ForOutline().Cut(Ring, obstacles, out _));
            Assert.Equal(Ring, forced.ForOutline().Cut(Ring, obstacles, out int touched));
            Assert.Equal(0, touched);
            Assert.True(forced.Build(float2.zero, Ring, obstacles, p => true).CanPaintAt(new float2(80, 0)));
            var fill = AreaFill.Fill(float2.zero, (min, max, into) => into.AddRange(obstacles.Where(o => forced.ForOutline().Stops(o.Kind))),
                forced.WaterOn ? p => true : (Func<float2,bool>)null, new FillOptions { Reach = 40, Gap = 10 });
            Assert.NotNull(fill.Ring);
            Assert.True(Poly.Contains(fill.Ring, float2.zero));
            var restored = UiModels.ParseBrushRules("{\"forceSelection\":false}", forced);
            Assert.Equal(defaults, restored);
            Assert.True(restored.LockOn);
        }

        [Fact]
        public void ForceIsPartOfUiRoundTripAndWallCacheIdentity()
        {
            var normal = new BrushRules { Anything = true };
            var forced = normal.Clone(); forced.ForceSelection = true;
            Assert.NotEqual(normal, forced);
            Assert.False(normal.ForOutline().SameWalls(forced.ForOutline()));
            Assert.NotEqual(normal.GetHashCode(), forced.GetHashCode());
            Assert.Equal(forced, UiModels.ParseBrushRules(UiModels.BrushRulesJson(forced).ToString(), BrushRules.Defaults()));
            Assert.True(normal.ForOutline().Stops(BrushObstacleKind.Road));
            Assert.False(forced.ForOutline().Stops(BrushObstacleKind.Road));
        }
    }
}
