using System;
using System.Collections.Generic;
using FlowerPainter.Core;
using FlowerPainter.Core.Build;
using FlowerPainter.Core.Geometry;
using FlowerPainter.Core.Layers.Roads;
using Unity.Mathematics;
using Xunit;

namespace FlowerPainter.Tests
{
    public class BrushAreaEditTests
    {
        private static Region Rect(float x, float y, float w, float h) => new Region(new[]
        { new float2(x,y), new float2(x+w,y), new float2(x+w,y+h), new float2(x,y+h) });
        private static List<Region> One(Region r) => new List<Region> { r };
        private static float Area(IEnumerable<Region> rs) { float a = 0; foreach(var r in rs) a += r.Area; return a; }

        [Fact] public void InteriorEraseKeepsHoleAndExterior()
        {
            var after = BrushAreaEdit.Change(One(Rect(0,0,100,100)), One(Rect(40,40,20,20)), true);
            Assert.Single(after); Assert.Single(after[0].Holes);
            Assert.False(BrushAreaEdit.Contains(after, new float2(50,50)));
            Assert.True(BrushAreaEdit.Contains(after, new float2(10,10)));
            Assert.Equal(9600f, Area(after), 1);
        }
        [Fact] public void ErasingAcrossMiddlePreservesBothPieces()
        {
            var after = BrushAreaEdit.Change(One(Rect(0,0,100,100)), One(Rect(45,-5,10,110)), true);
            Assert.Equal(2, after.Count); Assert.Equal(9000f, Area(after), 1);
            Assert.True(BrushAreaEdit.Contains(after, new float2(10,50)));
            Assert.True(BrushAreaEdit.Contains(after, new float2(90,50)));
        }
        [Fact] public void ExtensionOnlyPlansNewGround()
        {
            var old = One(Rect(0,0,100,100)); var brush = One(Rect(90,30,30,40));
            var added = BrushAreaEdit.Added(old, brush);
            Assert.Equal(800f, Area(added), 1);
            Assert.False(BrushAreaEdit.Contains(added, new float2(95,50)));
            Assert.True(BrushAreaEdit.Contains(added, new float2(110,50)));
        }
        [Fact] public void UnaffectedCurveIsIdentical()
        {
            var n = new PlannedNet { A = new float3(0,4,0), B = new float3(20,6,30), C = new float3(60,8,30), D = new float3(80,4,0) };
            var kept = Assert.Single(BrushAreaEdit.Clip(n, One(Rect(200,200,20,20)), false));
            Assert.Equal(n.A, kept.A); Assert.Equal(n.B, kept.B); Assert.Equal(n.C, kept.C); Assert.Equal(n.D, kept.D);
        }
        [Fact] public void CrossingCurveKeepsBothOriginalSubcurves()
        {
            var c = RoadCurve.Straight(new float3(0,5,50), new float3(100,5,50));
            var n = new PlannedNet { A=c.A,B=c.B,C=c.C,D=c.D };
            var kept = BrushAreaEdit.Clip(n, One(Rect(40,0,20,100)), false);
            Assert.Equal(2, kept.Count);
            Assert.InRange(kept[0].D.x,39.99f,40.01f); Assert.InRange(kept[1].A.x,59.99f,60.01f);
            Assert.Equal(c.A,kept[0].A); Assert.Equal(c.D,kept[1].D);
        }
        [Fact] public void FootprintSurvivesReceiptRoundTrip()
        {
            var regions = BrushAreaEdit.Change(One(Rect(0,0,100,100)), One(Rect(40,40,20,20)), true);
            var decoded = ReceiptCodec.Decode(ReceiptCodec.Encode(new GroupRecord { GroupId=7, BrushRegions=regions }));
            Assert.NotNull(decoded); Assert.Single(decoded.BrushRegions[0].Holes);
            Assert.Equal(9600f,Area(decoded.Footprint()),1);
            Assert.False(BrushAreaEdit.Contains(decoded.Footprint(),new float2(50,50)));
        }
        [Fact] public void EmptyFootprintDoesNotFallBackToOldOutline()
        {
            var record = new GroupRecord { Area = new AreaShape { Ring=Poly.To3D(Rect(0,0,100,100).Outer,_=>0) }, BrushRegions=new List<Region>() };
            var decoded=ReceiptCodec.Decode(ReceiptCodec.Encode(record));
            Assert.Empty(decoded.Footprint());
        }
        [Fact] public void ExtensionClipsShapePaintAndDoesNotTerraformOldGround()
        {
            var layer = new AreaPlan();
            var ring = Poly.To3D(Rect(0,0,100,100).Outer, _=>0);
            layer.Paint.Add(new PlannedPaint { Ring=ring, Shape=Core.Paint.PaintShape.Polygon(Poly.To2D(ring),0.01f) });
            var source = new ComposedPlan(); source.Layers.Add(layer);
            var patch = BrushAreaEdit.ClipPlan(source, One(Rect(90,0,10,100)), _=>0);
            var paint = Assert.Single(Assert.Single(patch.Layers).Paint);
            Assert.Equal(0f, PaintField.StrengthAt(paint, new float2(89.9f,50)));
            Assert.True(PaintField.StrengthAt(paint, new float2(95,50)) > .99f);
        }
        [Fact] public void TrimmingRetainsCurveModeUpgradesAndSurvivingAttachments()
        {
            var c = RoadCurve.Straight(new float3(0,5,50), new float3(100,5,50));
            var n = new PlannedNet { A=c.A,B=c.B,C=c.C,D=c.D, PreserveCurve=true, StartAttach=10, EndAttach=20, Upgrades=new[]{"left:Trees"} };
            var kept=BrushAreaEdit.Clip(n,One(Rect(40,0,20,100)),false);
            Assert.True(kept[0].PreserveCurve); Assert.Equal(n.Upgrades,kept[0].Upgrades);
            Assert.Equal(10,kept[0].StartAttach); Assert.Equal(-1,kept[0].EndAttach);
            Assert.Equal(-1,kept[1].StartAttach); Assert.Equal(20,kept[1].EndAttach);
        }
    }
}
