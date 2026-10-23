// Residential frontage stays zonable; gardens and public access occupy separate, explicit footprints.
using System;
using System.Collections.Generic;
using System.Threading;
using FlowerPainter.Core.Geometry;
using FlowerPainter.Core.Layers.Common;
using Unity.Mathematics;

namespace FlowerPainter.Core.Layers.Roads
{
    public static class NeighborhoodLayout
    {
        public const string Target = "neighborhoodGreen";
        public const float AccessWidth = 8f;
        public const int MaxGardens = 24;

        public static bool IsResidential(string kind) => kind == ZoneKinds.ResidentialLow || kind == ZoneKinds.ResidentialRow
            || kind == ZoneKinds.ResidentialMedium || kind == ZoneKinds.ResidentialHigh || kind == ZoneKinds.LowRent || kind == ZoneKinds.Mixed;

        internal static void AddRegions(AreaPlan plan, List<RoadBlocks.Block> blocks, List<RoadBlocks.Line> roads,
            int depth, Func<float2, float> height, CancellationToken cancel)
        {
            int count = 0, skipped = 0;
            foreach (var block in blocks)
            {
                cancel.ThrowIfCancellationRequested();
                if (block.RoundaboutIsland) continue;
                var gardens = Gardens(block.Ring, depth, roads, cancel);
                if (gardens.Count == 0) { skipped++; continue; }
                foreach (var ring in gardens)
                {
                    if (count >= MaxGardens) break;
                    plan.Regions.Add(new PlannedRegion { Key = Target + ":" + count++, Tags = new[] { Target }, Ring = Poly.To3D(ring, height) });
                }
            }
            plan.Facts["neighborhood.gardens"] = count;
            if (count == 0) plan.Issues.Add(new PlanIssue { Severity = IssueSeverity.Warning, Key = "FlowerPainter.Neighborhood.Issue.NoSpace" });
            else if (skipped > 0) plan.Issues.Add(new PlanIssue { Severity = IssueSeverity.Info, Key = "FlowerPainter.Neighborhood.Issue.Skipped", Args = new object[] { skipped } });
            if (count >= MaxGardens) plan.Issues.Add(new PlanIssue { Severity = IssueSeverity.Info, Key = "FlowerPainter.Neighborhood.Issue.Limit", Args = new object[] { MaxGardens } });
        }

        internal static List<float2[]> Gardens(float2[] block, int depth, List<RoadBlocks.Line> roads, CancellationToken cancel)
        {
            var output = new List<float2[]>();
            // Zone cells are real 8 m cells, independent of the decorative layout-scale setting.
            foreach (var inner in Poly.Offset(block, math.clamp(depth, 2, 6) * 8f + 2f))
            {
                cancel.ThrowIfCancellationRequested();
                var garden = LayerRegion.From(inner);
                if (garden == null || garden.Area < 100f) continue;
                var anchor = garden.DeepestPoint(out float radius);
                if (radius < 5f) continue;
                var candidates = new List<(float length, float2 from, float2 to)>();
                for (int i = 0; i < block.Length; i++)
                {
                    var a = block[i]; var b = block[(i + 1) % block.Length];
                    float length = math.distance(a, b);
                    if (length < AccessWidth + 4f) continue;
                    var from = (a + b) * 0.5f;
                    if (!NearStreet(from, roads)) continue;
                    var to = garden.ClosestOnBoundary(from, out _, out _);
                    var direction = math.normalizesafe(to - from);
                    var edge = math.normalizesafe(b - a);
                    if (math.abs(math.dot(direction, edge)) > 0.35f) continue;
                    bool inside = true;
                    for (int j = 1; j < 12; j++) if (!Poly.Contains(block, math.lerp(from, to, j / 12f))) { inside = false; break; }
                    if (inside) candidates.Add((length, from, to));
                }
                candidates.Sort((a, b) =>
                {
                    int order = b.length.CompareTo(a.length);
                    if (order != 0) return order;
                    order = a.from.x.CompareTo(b.from.x);
                    return order != 0 ? order : a.from.y.CompareTo(b.from.y);
                });
                var shapes = new List<IReadOnlyList<float2>> { inner };
                var chosen = new List<float2>();
                foreach (var candidate in candidates)
                {
                    bool apart = true;
                    foreach (var old in chosen) if (math.distance(old, candidate.from) < 20f) apart = false;
                    if (!apart) continue;
                    var direction = math.normalizesafe(candidate.to - candidate.from);
                    var side = new float2(-direction.y, direction.x) * (AccessWidth * 0.5f);
                    var a = candidate.from - direction * 0.2f;
                    var b = candidate.to + direction * 3f;
                    var corridor = Poly.EnsureCcw(new[] { a + side, a - side, b - side, b + side });
                    // A concave corner must not let an access shortcut leave the street block.
                    foreach (var clipped in Poly.Intersection(block, corridor)) shapes.Add(clipped.Outer);
                    chosen.Add(candidate.from);
                    if (chosen.Count == 2) break;
                }
                if (chosen.Count == 0) continue;
                foreach (var joined in Poly.UnionAll(shapes))
                {
                    if (joined.Holes.Count > 0 || !Poly.Contains(joined.Outer, anchor)) continue;
                    output.Add(joined.Outer);
                }
            }
            return output;
        }

        private static bool NearStreet(float2 p, List<RoadBlocks.Line> roads)
        {
            foreach (var road in roads)
                for (int i = 1; i < road.Points.Count; i++)
                    if (ReservedSpace.DistSqToSegment(p, road.Points[i - 1], road.Points[i]) <= math.pow(road.HalfWidth + 3f, 2f)) return true;
            return false;
        }
    }
}
