using System.Collections.Generic;
using System.Threading;
using FlowerPainter.Core.Geometry;
using FlowerPainter.Core.Layers.Common;
using Unity.Mathematics;

namespace FlowerPainter.Core.Layers.Types.Floral
{
    // Zero-width cuts: use the same planar arrangement as polygon splitting, including
    // all lines at once so intersecting curves and loops assembled from several cuts work.
    public sealed class FloralPartitions
    {
        public readonly List<Region> Regions;
        private readonly float2[] _min, _max;

        public FloralPartitions(LayerRegion frame, List<FloralMotif> motifs, CancellationToken cancel)
        {
            var lines = new List<IReadOnlyList<float2>> { frame.Ring };
            var origin = FloralMotif.Origin(frame);
            foreach (var motif in motifs)
            {
                cancel.ThrowIfCancellationRequested();
                if (motif.Kind == "curve" && motif.Points.Length < 2) continue;
                lines.Add(motif.Outline(origin));
            }
            var arrangement = Arrangement.For(lines);
            arrangement.AddRing(frame.Ring, 0);
            for (int i = 1; i < lines.Count; i++) arrangement.AddPolyline(lines[i]);
            cancel.ThrowIfCancellationRequested();
            Regions = arrangement.Extract((a, b) => a != 0, splitByCuts: true);
            cancel.ThrowIfCancellationRequested();
            _min = new float2[Regions.Count]; _max = new float2[Regions.Count];
            for (int i = 0; i < Regions.Count; i++) Poly.Bounds(Regions[i].Outer, out _min[i], out _max[i]);
        }

        public int At(float2 p)
        {
            for (int i = 0; i < Regions.Count; i++)
                if (math.all(p >= _min[i]) && math.all(p <= _max[i]) && Regions[i].Contains(p)) return i + 1;
            return -1;
        }
    }
}
