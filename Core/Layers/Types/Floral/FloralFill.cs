using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using FlowerPainter.Core.Layers.Common;
using Unity.Mathematics;

namespace FlowerPainter.Core.Layers.Types.Floral
{
    public sealed class FloralFill
    {
        public float X, Z;
        public string Style = "flow", Flower = "";
        public string Encode() => string.Join("|", "v1", X.ToString("R", CultureInfo.InvariantCulture), Z.ToString("R", CultureInfo.InvariantCulture), Style, Uri.EscapeDataString(Flower));
        public static List<FloralFill> Read(string[] values)
        {
            var list = new List<FloralFill>();
            foreach (var text in values ?? Array.Empty<string>()) {
                var a = text.Split('|');
                if (a.Length != 5 || a[0] != "v1" || (a[3] != "flow" && a[3] != "islands" && a[3] != "single" && a[3] != "empty")) continue;
                if (!float.TryParse(a[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) || !float.TryParse(a[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z) || !math.isfinite(x) || !math.isfinite(z)) continue;
                try { list.Add(new FloralFill { X = x, Z = z, Style = a[3], Flower = Uri.UnescapeDataString(a[4]) }); } catch (UriFormatException) { }
                if (list.Count == 64) break;
            }
            return list;
        }
    }

    // Connected empty regions, separated by the planted ribbons. Seeds live in the same
    // local coordinates as motifs, so circle interiors can be filled independently of the outside.
    public sealed class FloralRegions
    {
        private readonly float2 _min, _origin;
        private readonly float _pitch;
        private readonly int _nx, _nz;
        private readonly int[] _labels;
        public FloralRegions(LayerRegion region, List<FloralMotif> motifs, CancellationToken cancel)
        {
            _min = region.Min; _origin = FloralMotif.Origin(region);
            float width = 2;
            foreach (var m in motifs) width = math.min(width, m.Width);
            var span = region.Max - region.Min;
            _pitch = math.max(width / 3, math.sqrt(span.x * span.y / 250000f));
            _pitch = math.max(0.2f, _pitch);
            _nx = math.max(1, (int)math.ceil((region.Max.x - _min.x) / _pitch));
            _nz = math.max(1, (int)math.ceil((region.Max.y - _min.y) / _pitch));
            _labels = new int[_nx * _nz];
            for (int z = 0; z < _nz; z++) {
                cancel.ThrowIfCancellationRequested();
                for (int x = 0; x < _nx; x++) {
                    var p = _min + new float2(x + 0.5f, z + 0.5f) * _pitch;
                    if (!region.Contains(p)) { _labels[z * _nx + x] = -1; continue; }
                    foreach (var m in motifs) if (m.Covers(p, _origin)) { _labels[z * _nx + x] = -1; break; }
                }
            }
            var queue = new Queue<int>(); int label = 0;
            for (int seed = 0; seed < _labels.Length; seed++) {
                if (_labels[seed] != 0) continue;
                label++; _labels[seed] = label; queue.Enqueue(seed);
                while (queue.Count > 0) {
                    int at = queue.Dequeue(), x = at % _nx, z = at / _nx;
                    if ((at & 255) == 0) cancel.ThrowIfCancellationRequested();
                    void Visit(int n) { if (_labels[n] == 0) { _labels[n] = label; queue.Enqueue(n); } }
                    if (x > 0) Visit(at - 1); if (x + 1 < _nx) Visit(at + 1);
                    if (z > 0) Visit(at - _nx); if (z + 1 < _nz) Visit(at + _nx);
                }
            }
        }
        public int At(float2 p) {
            int x = (int)math.floor((p.x - _min.x) / _pitch), z = (int)math.floor((p.y - _min.y) / _pitch);
            return x < 0 || z < 0 || x >= _nx || z >= _nz ? -1 : _labels[z * _nx + x];
        }
    }
}
