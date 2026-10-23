using System.Collections.Generic;
using Unity.Mathematics;

namespace FlowerPainter.Core.Geometry
{
    /// <summary>Rectangle constraints in the map plane; heights are sampled by the game tool.</summary>
    public static class RectangleSelection
    {
        public static float2 Next(IReadOnlyList<float2> points, float2 wanted)
        {
            if (points.Count < 2) return wanted;
            if (points.Count >= 3) return points[0] + points[2] - points[1];
            var edge = math.normalizesafe(points[1] - points[0], new float2(1, 0));
            var normal = new float2(-edge.y, edge.x);
            return points[1] + normal * math.dot(wanted - points[1], normal);
        }

        public static float2[] Fit(IReadOnlyList<float2> points, float minEdge = .5f)
        {
            if (points == null || points.Count < 2) return null;
            var origin = points[0];
            var x = math.normalizesafe(points[1] - origin, new float2(1, 0));
            var y = new float2(-x.y, x.x);
            float2 min = float2.zero, max = float2.zero;
            foreach (var p in points)
            {
                var uv = new float2(math.dot(p - origin, x), math.dot(p - origin, y));
                min = math.min(min, uv); max = math.max(max, uv);
            }
            max = math.max(max, min + minEdge);
            return new[] { origin + min.x*x + min.y*y, origin + max.x*x + min.y*y,
                origin + max.x*x + max.y*y, origin + min.x*x + max.y*y };
        }

        public static float2[] MoveCorner(IReadOnlyList<float2> points, int index, float2 wanted, float minEdge = .5f)
        {
            if (points == null || points.Count != 4 || index < 0 || index > 3) return null;
            int opposite = (index + 2) % 4, next = (index + 1) % 4, previous = (index + 3) % 4;
            var anchor = points[opposite];
            var x = math.normalizesafe(points[next] - anchor);
            var y = new float2(-x.y, x.x);
            if (math.dot(y, points[previous] - anchor) < 0) y = -y;
            float width = math.max(minEdge, math.dot(wanted - anchor, x));
            float height = math.max(math.max(minEdge, 1.1f / width), math.dot(wanted - anchor, y));
            var a = x * width;
            var b = y * height;
            var ring = new float2[4];
            ring[opposite] = anchor; ring[next] = anchor + a; ring[previous] = anchor + b; ring[index] = anchor + a + b;
            return ring;
        }

        public static bool IsRectangle(IReadOnlyList<float2> points)
        {
            if (points == null || points.Count != 4) return false;
            for (int i = 0; i < 4; i++)
            {
                var a = points[(i + 1) % 4] - points[i];
                var b = points[(i + 2) % 4] - points[(i + 1) % 4];
                if (math.lengthsq(a) < .24f || math.abs(math.dot(math.normalizesafe(a), math.normalizesafe(b))) > .001f) return false;
            }
            return Poly.IsSimple(points);
        }
    }
}
