using System;
using System.Collections.Generic;
using System.Globalization;
using FlowerPainter.Core.Layers.Common;
using Unity.Mathematics;

namespace FlowerPainter.Core.Layers.Types.Floral
{
    // Positions are metres relative to the selection bounds' centre. A motif never shrinks or
    // moves itself to fit the polygon: clipping happens only at the planting samples.
    public sealed class FloralMotif
    {
        public string Id = "", Kind = "ring", Flower = "";
        public float X, Z, Size = 40, Angle, Width = 4;
        public float2[] Points = Array.Empty<float2>();
        private float2[] _path;
        public static bool ValidKind(string kind) => kind == "ring" || kind == "wave" || kind == "petal" || kind == "disc" || kind == "heart" || kind == "curve";
        private static string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);
        public string Encode() {
            var basic = string.Join("|", Kind == "curve" ? "v2" : "v1", Uri.EscapeDataString(Id), Kind, F(X), F(Z), F(Size), F(Angle), F(Width), Uri.EscapeDataString(Flower));
            if (Kind != "curve") return basic;
            var parts = new List<string>(); foreach (var p in Points) parts.Add(F(p.x) + "," + F(p.y));
            return basic + "|" + string.Join(";", parts);
        }
        public FloralMotif Clone() { var m = (FloralMotif)MemberwiseClone(); m.Points = (float2[])Points.Clone(); m._path = null; return m; }
        public static FloralMotif Parse(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            var a = value.Split('|');
            bool curve = a.Length == 10 && a[0] == "v2" && a[2] == "curve";
            if ((!curve && (a.Length != 9 || a[0] != "v1")) || !ValidKind(a[2])) return null;
            var f = new float[5];
            for (int i = 0; i < 5; i++)
                if (!float.TryParse(a[3 + i], NumberStyles.Float, CultureInfo.InvariantCulture, out f[i]) || !math.isfinite(f[i])) return null;
            try
            {
                var points = new List<float2>();
                if (curve && a[9].Length > 0) foreach (var pair in a[9].Split(';')) {
                    var xy = pair.Split(',');
                    if (xy.Length != 2 || !float.TryParse(xy[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) || !float.TryParse(xy[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var z) || !math.isfinite(x) || !math.isfinite(z)) return null;
                    points.Add(new float2(math.clamp(x, -100, 100), math.clamp(z, -100, 100))); if (points.Count > 64) return null;
                }
                return new FloralMotif { Id = Uri.UnescapeDataString(a[1]), Kind = a[2], X = math.clamp(f[0], -4000, 4000), Z = math.clamp(f[1], -4000, 4000), Points = points.ToArray(),
                    Size = math.clamp(f[2], 2, 2000), Angle = f[3] % 360, Width = math.clamp(f[4], 0.5f, 100), Flower = Uri.UnescapeDataString(a[8]) };
            }
            catch (UriFormatException) { return null; }
        }
        public static List<FloralMotif> Read(string[] values)
        {
            var motifs = new List<FloralMotif>();
            foreach (var text in values ?? Array.Empty<string>())
            {
                var motif = Parse(text);
                if (motif != null) motifs.Add(motif);
                if (motifs.Count >= 32) break;
            }
            return motifs;
        }
        public static float2 Origin(LayerRegion region) => (region.Min + region.Max) * 0.5f;
        public float2 Center(float2 origin) => origin + new float2(X, Z);
        public float2 World(float2 local, float2 origin)
        {
            math.sincos(math.radians(Angle), out float s, out float c);
            return Center(origin) + new float2(local.x * c - local.y * s, local.x * s + local.y * c);
        }
        public bool Covers(float2 p, float2 origin)
        {
            if (Kind == "curve" && Points.Length < 2) return false;
            var d = p - Center(origin);
            math.sincos(math.radians(Angle), out float s, out float c);
            var q = new float2(d.x * c + d.y * s, -d.x * s + d.y * c);
            float r = Size * 0.5f;
            if (Kind == "curve" || Kind == "heart") {
                if (_path == null) _path = Outline(float2.zero, 96);
                for (int i = 1; i < _path.Length; i++) {
                    var a = _path[i - 1] - Center(float2.zero); var b = _path[i] - Center(float2.zero);
                    var d0 = p - origin - Center(float2.zero);
                    var ab = b - a; float t = math.saturate(math.dot(d0 - a, ab) / math.max(1e-8f, math.lengthsq(ab)));
                    if (math.distancesq(d0, a + ab * t) <= Width * Width * 0.25f) return true;
                }
                return false;
            }
            if (Kind == "disc") return math.lengthsq(q) <= r * r;
            if (Kind == "wave")
            {
                if (math.abs(q.x) > r) return false;
                float t = (q.x / Size + 0.5f) * math.PI * 2f;
                float y = math.sin(t) * Size * 0.22f, slope = math.cos(t) * math.PI * 0.44f;
                return math.abs(q.y - y) / math.sqrt(1f + slope * slope) <= Width * 0.5f;
            }
            if (Kind == "petal") r *= 0.72f + 0.28f * math.cos(5f * math.atan2(q.y, q.x));
            return math.abs(math.length(q) - r) <= Width * 0.5f;
        }
        public float2[] Outline(float2 origin, int count = 128)
        {
            var p = new float2[count + 1];
            for (int i = 0; i <= count; i++)
            {
                float t = i / (float)count, a = t * math.PI * 2f, r = Size * 0.5f;
                if (Kind == "curve") {
                    if (Points.Length < 2) { p[i] = Center(origin); continue; }
                    float u = t * (Points.Length - 1); int k = math.min((int)u, Points.Length - 2); float v = u - k;
                    var p0 = Points[math.max(0,k-1)]; var p1 = Points[k]; var p2 = Points[k+1]; var p3 = Points[math.min(Points.Length-1,k+2)];
                    var local = 0.5f * ((2*p1) + (-p0+p2)*v + (2*p0-5*p1+4*p2-p3)*v*v + (-p0+3*p1-3*p2+p3)*v*v*v);
                    p[i] = World(local * Size, origin); continue;
                }
                if (Kind == "heart") { p[i] = World(new float2(16 * math.pow(math.sin(a),3), 13*math.cos(a)-5*math.cos(2*a)-2*math.cos(3*a)-math.cos(4*a)) * (Size/34f), origin); continue; }
                if (Kind == "petal") r *= 0.72f + 0.28f * math.cos(5f * a);
                p[i] = World(Kind == "wave" ? new float2((t - 0.5f) * Size, math.sin(a) * Size * 0.22f) : new float2(math.cos(a), math.sin(a)) * r, origin);
            }
            return p;
        }
        public static string[] Example(int index)
        {
            FloralMotif M(string id, string kind, float x, float z, float size, float angle, float width) => new FloralMotif { Id = id, Kind = kind, X = x, Z = z, Size = size, Angle = angle, Width = width };
            FloralMotif[] motifs = index == 2 ? new[] { M("a", "wave", -5, 7, 70, 22, 5), M("b", "petal", 27, -18, 52, -20, 4) }
                : index == 3 ? new[] { M("a", "ring", -38, 14, 80, 0, 6), M("b", "ring", 40, -12, 78, 0, 4), M("c", "wave", 0, -6, 74, -25, 3) }
                : new[] { M("a", "ring", -40, 5, 90, 0, 6), M("b", "wave", 3, -6, 80, -18, 5) };
            var encoded = new string[motifs.Length];
            for (int i = 0; i < encoded.Length; i++) encoded[i] = motifs[i].Encode();
            return encoded;
        }
    }
}
