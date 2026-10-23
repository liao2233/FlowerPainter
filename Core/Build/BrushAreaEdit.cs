using System;
using System.Collections.Generic;
using FlowerPainter.Core.Geometry;
using FlowerPainter.Core.Layers.Roads;
using Unity.Mathematics;

namespace FlowerPainter.Core.Build
{
    /// <summary>Local edits keep the old geometry; only the difference is planned or removed.</summary>
    public static class BrushAreaEdit
    {
        public static bool Contains(IEnumerable<Region> regions, float2 p)
        {
            if (regions == null) return false;
            foreach (var r in regions)
            {
                if (!Poly.Contains(r.Outer, p)) continue;
                bool hole = false;
                foreach (var h in r.Holes) if (Poly.Contains(h, p)) { hole = true; break; }
                if (!hole) return true;
            }
            return false;
        }

        public static List<Region> Change(List<Region> old, List<Region> stroke, bool erase) =>
            Poly.Boolean(old, stroke, erase ? BoolOp.Difference : BoolOp.Union);

        public static List<Region> Added(List<Region> old, List<Region> stroke) => Poly.Boolean(stroke, old, BoolOp.Difference);

        /// <summary>Actual polygons, preserving every island and hole (no "largest piece" shortcut).</summary>
        public static List<float3[]> Rings(List<Region> regions, Func<float2, float> height)
        {
            var result = new List<float3[]>();
            foreach (var r in regions)
                foreach (var ring in SurfaceAcceptance.MakeAcceptable2D(r, out _)) result.Add(Poly.To3D(ring, height));
            return result;
        }

        public static List<float3[]> Clip(float3[] ring, List<Region> region, bool keepInside, Func<float2, float> height)
        {
            var source = new List<Region> { new Region(Poly.To2D(ring)) };
            return Rings(Poly.Boolean(source, region, keepInside ? BoolOp.Intersection : BoolOp.Difference), height);
        }

        /// <summary>Exact sub-curves, with boundary transitions located to well below a centimetre.</summary>
        public static List<PlannedNet> Clip(PlannedNet net, List<Region> regions, bool keepInside)
        {
            var curve = new RoadCurve(net.A, net.B, net.C, net.D);
            int samples = math.clamp((int)math.ceil(curve.LengthXZ(32) * 2f), 16, 32768);
            bool Keep(float t) => Contains(regions, curve.Position(t).xz) == keepInside;
            var result = new List<PlannedNet>();
            bool was = Keep(0f);
            float start = 0f;
            for (int i = 1; i <= samples; i++)
            {
                float t = i / (float)samples;
                bool now = Keep(t);
                if (now == was) continue;
                float a = (i - 1f) / samples, b = t;
                for (int j = 0; j < 16; j++) { float m = (a + b) * 0.5f; if (Keep(m) == was) a = m; else b = m; }
                float at = (a + b) * 0.5f;
                if (was) Add(start, at); else start = at;
                was = now;
            }
            if (was) Add(start, 1f);
            return result;

            void Add(float a, float b)
            {
                var c = curve.Sub(a, b);
                if (c.LengthXZ() < 0.25f) return;
                result.Add(new PlannedNet
                {
                    A = c.A, B = c.B, C = c.C, D = c.D, Asset = net.Asset, Role = net.Role,
                    Control = net.Control, Category = net.Category, FollowTerrain = net.FollowTerrain, PreserveCurve = net.PreserveCurve, Upgrades = net.Upgrades,
                    StartAttach = a < 1e-5f ? net.StartAttach : -1, EndAttach = b > 1f - 1e-5f ? net.EndAttach : -1,
                    StartAttachT = net.StartAttachT, EndAttachT = net.EndAttachT,
                });
            }
        }

        /// <summary>Restrict a generated extension to new land. Existing content is never regenerated.</summary>
        public static ComposedPlan ClipPlan(ComposedPlan source, List<Region> added, Func<float2, float> height)
        {
            var result = new ComposedPlan();
            foreach (var issue in source.Issues)
                if (!issue.At.HasValue || Contains(added, issue.At.Value.xz)) result.Issues.Add(issue);
            foreach (var l in source.Layers)
            {
                var p = new AreaPlan { GeneratorId = l.GeneratorId, StepPath = l.StepPath, Seed = l.Seed };
                foreach (var issue in l.Issues)
                    if (!issue.At.HasValue || Contains(added, issue.At.Value.xz)) p.Issues.Add(issue);
                foreach (var o in l.Objects) if (Contains(added, o.Position.xz)) { o.AttachedNet = -1; p.Objects.Add(o); }
                foreach (var n in l.Nets) p.Nets.AddRange(Clip(n, added, true));
                foreach (var s in l.Surfaces)
                    foreach (var ring in Clip(s.Ring, added, true, height)) p.Surfaces.Add(new PlannedSurface { Ring = ring, Role = s.Role, Asset = s.Asset, Control = s.Control, Category = s.Category });
                foreach (var a in l.Areas)
                    foreach (var ring in Clip(a.Ring, added, true, height)) p.Areas.Add(new PlannedArea { Ring = ring, Role = a.Role, Asset = a.Asset, Control = a.Control });
                foreach (var paint in l.Paint)
                    foreach (var ring in Clip(paint.Ring, added, true, height))
                    {
                        var clipped = paint.WithRing(ring);
                        // Legacy feather straddles the boundary; a local edit must fade only inwards.
                        clipped.Feather = math.max(0.01f, clipped.Feather);
                        if (clipped.Shape != null) clipped.Shape = clipped.Shape.Mul(Paint.PaintShape.Polygon(Poly.To2D(ring), 0.01f));
                        p.Paint.Add(clipped);
                    }
                // Terraform banks extend outside their polygon. Local brush edits use the existing ground instead.
                foreach (var o in p.Objects) o.Position.y = height(o.Position.xz);
                foreach (var z in l.Zoning)
                {
                    var quad = new Region(new[] { z.A.xz, z.B.xz, z.C.xz, z.D.xz });
                    var outside = Poly.Boolean(new[] { quad }, added, BoolOp.Difference);
                    double area = 0; foreach (var r in outside) area += r.Area;
                    if (area < 0.01) p.Zoning.Add(z);
                }
                result.Layers.Add(p);
            }
            foreach (var r in added) foreach (var ring in SurfaceAcceptance.MakeAcceptable2D(r, out _)) result.Footprints.Add(ring);
            return result;
        }
    }
}
