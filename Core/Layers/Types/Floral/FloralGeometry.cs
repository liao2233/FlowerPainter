using System;
using System.Collections.Generic;
using System.Threading;
using FlowerPainter.Core.Geometry;
using FlowerPainter.Core.Layers.Common;
using FlowerPainter.Core.Tool;
using Unity.Mathematics;

namespace FlowerPainter.Core.Layers.Types.Floral
{
    internal static class FloralGeometry
    {
        internal sealed class Stroke
        {
            public float2[] Points;
            public bool Closed;
            public int Band;
            public bool FilledRow;
        }

        internal static List<Stroke> Make(LayerRegion region, SettingsBag settings, List<EdgeMarker> markers,
            float margin, float pitch, CancellationToken cancel)
        {
            var result = new List<Stroke>();
            string shape = settings.S("shape", "ring");
            float width = math.clamp(settings.F("bandWidth", 4), 0.5f, 16f);
            float2 center = region.DeepestPoint(out float clearance);
            float radius = math.max(0f, clearance - margin - width * 0.5f) * math.clamp(settings.F("size", 85), 20f, 100f) / 100f;
            float angle = math.radians(settings.F("rotation", 0));
            float2 World(float2 p)
            {
                math.sincos(angle, out float sin, out float cos);
                return center + new float2(p.x * cos - p.y * sin, p.x * sin + p.y * cos);
            }
            float2[] Parametric(Func<float, float2> point, int steps, bool closed)
            {
                var points = new float2[steps + (closed ? 0 : 1)];
                for (int i = 0; i < points.Length; i++) { if ((i & 63) == 0) cancel.ThrowIfCancellationRequested(); points[i] = World(point(i / (float)steps)); }
                return points;
            }
            void Add(float2[] points, bool closed, int band = 0, bool filled = false)
            {
                if (points != null && points.Length >= 2) result.Add(new Stroke { Points = points, Closed = closed, Band = band, FilledRow = filled });
            }

            if (shape == "freeform")
            {
                var points = new List<float2>();
                foreach (var m in markers ?? new List<EdgeMarker>())
                {
                    if (m.Kind != MarkerKinds.FloralPoint || !region.Contains(m.Position.xz)) continue;
                    if (points.Count == 0 || math.distance(points[points.Count - 1], m.Position.xz) >= 0.2f) points.Add(m.Position.xz);
                    if (points.Count == 32) break;
                }
                if (points.Count >= 2) Add(Smooth(points, settings.S("curve", "smooth") == "smooth", cancel), false);
                else if (points.Count == 0 && radius > 1f)
                    Add(Parametric(t => new float2((t * 2f - 1f) * radius, math.sin(t * math.PI * 2f) * radius * 0.45f), 256, false), false);
                return result;
            }
            if (shape == "outline")
            {
                foreach (var ring in Poly.Offset(region.Ring, margin + width * 0.5f)) Add(ring, true);
                return result;
            }
            if (radius < 1f) return result;
            int samples = math.clamp((int)(radius * 12f), 128, 1024);
            switch (shape)
            {
                case "circle":
                    int rows = math.min(128, math.max(1, (int)math.ceil(radius / pitch)));
                    float step = radius / rows;
                    for (int i = 0; i < rows; i++)
                    {
                        cancel.ThrowIfCancellationRequested();
                        float r = radius - i * step;
                        Add(Parametric(t => new float2(math.cos(t * math.PI * 2f), math.sin(t * math.PI * 2f)) * r, samples, true), true, i, true);
                    }
                    Add(new[] { center, center + new float2(0.001f, 0f) }, false, rows, true);
                    break;
                case "rosette":
                    int petals = math.clamp(settings.I("petals", 6), 3, 12);
                    Add(Parametric(t =>
                    {
                        float a = t * math.PI * 2f;
                        float r = radius * (0.72f + 0.28f * math.cos(petals * a));
                        return new float2(math.cos(a), math.sin(a)) * r;
                    }, samples, true), true);
                    break;
                case "spiral":
                    float turns = math.clamp(settings.F("turns", 2.5f), 1f, 5f);
                    Add(Parametric(t =>
                    {
                        float a = t * math.PI * 2f * turns;
                        float r = radius * (0.1f + 0.9f * t);
                        return new float2(math.cos(a), math.sin(a)) * r;
                    }, samples, false), false);
                    break;
                case "heart":
                    Add(Parametric(t =>
                    {
                        float a = t * math.PI * 2f;
                        return new float2(16f * math.pow(math.sin(a), 3f),
                            13f * math.cos(a) - 5f * math.cos(2f * a) - 2f * math.cos(3f * a) - math.cos(4f * a) + 2.5f) / 17f * radius;
                    }, samples, true), true);
                    break;
                default:
                    Add(Parametric(t => new float2(math.cos(t * math.PI * 2f), math.sin(t * math.PI * 2f)) * radius, samples, true), true);
                    break;
            }
            return result;
        }

        private static float2[] Smooth(List<float2> points, bool curved, CancellationToken cancel)
        {
            if (!curved || points.Count == 2) return points.ToArray();
            var result = new List<float2>();
            for (int i = 0; i + 1 < points.Count; i++)
            {
                cancel.ThrowIfCancellationRequested();
                var a = points[math.max(0, i - 1)]; var b = points[i];
                var c = points[i + 1]; var d = points[math.min(points.Count - 1, i + 2)];
                // Clamped tangents keep short control spans from developing large loops.
                float span = math.distance(b, c);
                var m0 = ClampLength((c - a) * 0.5f, span);
                var m1 = ClampLength((d - b) * 0.5f, span);
                int n = math.clamp((int)math.ceil(span / 0.5f), 8, 256);
                for (int j = 0; j < n; j++)
                {
                    float t = j / (float)n, t2 = t * t, t3 = t2 * t;
                    result.Add((2 * t3 - 3 * t2 + 1) * b + (t3 - 2 * t2 + t) * m0 + (-2 * t3 + 3 * t2) * c + (t3 - t2) * m1);
                }
            }
            result.Add(points[points.Count - 1]);
            return result.ToArray();
        }

        private static float2 ClampLength(float2 p, float length) => math.normalizesafe(p) * math.min(math.length(p), length);

        internal static float Length(Stroke stroke)
        {
            float length = 0;
            for (int i = 1; i < stroke.Points.Length; i++) length += math.distance(stroke.Points[i - 1], stroke.Points[i]);
            if (stroke.Closed) length += math.distance(stroke.Points[stroke.Points.Length - 1], stroke.Points[0]);
            return length;
        }

        internal static IEnumerable<(float2 p, float2 tangent, float along)> Samples(Stroke stroke, float pitch, float offset, CancellationToken cancel)
        {
            var points = stroke.Points;
            float length = Length(stroke);
            int count = math.max(stroke.Closed ? 3 : 1, (int)math.ceil(length / pitch));
            float step = length / count;
            int edge = 0;
            float before = 0;
            int edgeCount = points.Length - (stroke.Closed ? 0 : 1);
            for (int i = 0; i < count + (stroke.Closed ? 0 : 1); i++)
            {
                if ((i & 63) == 0) cancel.ThrowIfCancellationRequested();
                float target = i * step;
                while (edge < edgeCount - 1 && before + math.distance(points[edge], points[(edge + 1) % points.Length]) < target)
                { before += math.distance(points[edge], points[(edge + 1) % points.Length]); edge++; }
                var a = points[edge]; var b = points[(edge + 1) % points.Length];
                float segment = math.distance(a, b);
                var tangent = math.normalizesafe(b - a, new float2(1f, 0f));
                var p = math.lerp(a, b, segment > 0.0001f ? math.saturate((target - before) / segment) : 0f);
                yield return (p + new float2(-tangent.y, tangent.x) * offset, tangent, length > 0 ? target / length : 0f);
            }
        }
    }
}
