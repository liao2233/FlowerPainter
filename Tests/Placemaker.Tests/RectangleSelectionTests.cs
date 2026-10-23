using FlowerPainter.Core.Geometry;
using FlowerPainter.Core.Tool;
using Unity.Mathematics;
using Xunit;

namespace FlowerPainter.Tests
{
    public class RectangleSelectionTests
    {
        private static PolygonEditor Draw(float angle, float width)
        {
            var e = new PolygonEditor(); e.SetRectangle(true);
            float2 x = new float2(math.cos(angle), math.sin(angle)), y = new float2(-x.y, x.x), origin = new float2(8000, -7000);
            foreach (var p in new[] { origin, origin + x*100, origin + x*137 + y*width, origin + x*29 + y*92 })
                Assert.True(e.AddPoint(new float3(p.x, 20, p.y)));
            Assert.True(e.Close());
            return e;
        }

        [Theory]
        [InlineData(0f, 65f)]
        [InlineData(.37f, 65f)]
        [InlineData(1.2f, -65f)]
        public void FourClicksArePerpendicularIncludingRotatedAndClockwise(float angle, float width)
        {
            var e = Draw(angle, width);
            Assert.True(RectangleSelection.IsRectangle(e.Ring2D()));
            Assert.InRange(math.distance(e.Points[0].xz, e.Points[1].xz),99.99f,100.01f);
            Assert.InRange(math.distance(e.Points[1].xz, e.Points[2].xz),64.99f,65.01f);
            Assert.False(e.RemovePoint(1));
            Assert.Equal(-1,e.InsertPoint(0,(e.Points[0]+e.Points[1])*.5f));
        }

        [Fact]
        public void DraggingAnyCornerKeepsOppositeCornerAndAllRightAngles()
        {
            for (int i=0;i<4;i++)
            {
                var e=Draw(.37f,65);
                var anchor=e.Points[(i+2)%4];
                var from=e.Points[i];
                e.MovePoint(i,from+new float3(12,0,-8),from);
                Assert.Equal(anchor,e.Points[(i+2)%4]);
                Assert.True(RectangleSelection.IsRectangle(e.Ring2D()));
                e.MovePoint(i,anchor,from);
                Assert.True(RectangleSelection.IsRectangle(e.Ring2D()));
                Assert.True(e.IsValid);
            }
        }

        [Fact]
        public void EdgesAndSnapshotRestoreRetainRectangleButSwitchingOffAllowsFreeEdits()
        {
            var e=Draw(.37f,65); var before=e.Save();
            Assert.True(e.BeginEdgeDrag(1)); e.DragEdge(12,true); e.EndEdgeDrag();
            Assert.True(RectangleSelection.IsRectangle(e.Ring2D()));
            e.SetRectangle(false);
            e.MovePoint(0,e.Points[0]+new float3(7,0,3),e.Points[0]);
            Assert.False(RectangleSelection.IsRectangle(e.Ring2D()));
            e.SetRectangle(true);
            Assert.True(RectangleSelection.IsRectangle(e.Ring2D()));
            e.Restore(before);
            Assert.True(e.Rectangle);
            Assert.Equal(before.Points,e.Save().Points);
            e.Clear(); Assert.True(e.Rectangle);
        }

        [Fact]
        public void ThreePointsCannotCloseAndRightClickCanUndoWidth()
        {
            var e=new PolygonEditor();e.SetRectangle(true);
            e.AddPoint(float3.zero);e.AddPoint(new float3(100,0,0));e.AddPoint(new float3(130,0,60));
            Assert.False(e.Close());Assert.True(e.RemoveLast());
            Assert.False(e.AddPoint(new float3(120,0,0)));
            Assert.True(e.AddPoint(new float3(140,0,-60)));
            Assert.True(e.AddPoint(new float3(23,0,-80)));Assert.True(e.Close());
            Assert.True(RectangleSelection.IsRectangle(e.Ring2D()));
        }

        [Fact]
        public void UiRoundTripDoesNotChangeOtherToolsStops()
        {
            var normal=BrushRules.Defaults();
            var rectangle=UiModels.ParseBrushRules("{\"rectangle\":true}",normal);
            Assert.True(rectangle.Rectangle);Assert.True(rectangle.SameWalls(normal));
            Assert.Equal(rectangle,UiModels.ParseBrushRules(UiModels.BrushRulesJson(rectangle).ToString(),normal));
        }
    }
}
