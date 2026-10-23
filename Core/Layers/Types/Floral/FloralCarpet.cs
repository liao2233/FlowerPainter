using System;
using System.Collections.Generic;
using System.Threading;
using FlowerPainter.Core.Geometry;
using FlowerPainter.Core.Layers.Common;
using Unity.Mathematics;

namespace FlowerPainter.Core.Layers.Types.Floral
{
    // Colour is a continuous field; sampling it never leaves the space between ribbons empty.
    internal static class FloralCarpet
    {
        internal const int MaxObjects = 3200;
        private const float CoveragePitch = 1.55f;
        internal static bool IsCarpet(string shape) => shape == "flow" || shape == "islands" || shape == "collage";

        internal sealed class Pattern
        {
            private readonly float2 _center, _axis, _cross;
            private readonly float _width, _length, _phase, _wave, _amplitude;
            private readonly bool _islands;
            private readonly int _shift, _direction, _colors;
            private readonly List<(float2 center, float2 radius, float angle, int slot)> _patches = new List<(float2, float2, float, int)>();

            internal Pattern(LayerRegion region, string shape, float width, float rotation, uint seed, int colors = 3)
            {
                var rng = Hashing.Rng(seed, "floral-carpet");
                region.PrincipalAxes(out _center, out var axis, out float length, out float breadth);
                float angle = math.atan2(axis.y, axis.x) + math.radians(rotation) + rng.NextFloat(-0.28f, 0.28f);
                _axis = new float2(math.cos(angle), math.sin(angle));
                _cross = new float2(-_axis.y, _axis.x);
                // Scale the composition with the plot so a large selection still has broad
                // colour masses instead of dozens of thin stripes or tiny isolated dots.
                _colors = math.max(1, colors);
                float designWidth = math.max(2f, width) * math.max(1f, math.sqrt(region.Area / 1800f));
                _width = _colors <= 3 ? designWidth : math.min(designWidth,
                    shape == "islands" ? math.sqrt(region.Area / (_colors * 10f)) : breadth / (_colors + 1f));
                _width = math.max(0.25f, _width);
                _length = math.max(length, _width * 4f);
                _phase = rng.NextFloat(0, math.PI * 2f);
                _wave = rng.NextFloat(0.65f, 1.35f);
                _amplitude = rng.NextFloat(0.6f, 1.35f) * _width;
                _shift = rng.NextInt(0, _colors);
                _direction = rng.NextBool() ? 1 : -1;
                _islands = shape == "islands";
                if (!_islands || _colors == 1) return;
                int count = math.max(_colors - 1, math.clamp((int)(region.Area / (_width * _width * 6f)), 3, 12));
                for (int attempt = 0; attempt < count * 80 && _patches.Count < count; attempt++)
                {
                    var p = rng.NextFloat2(region.Min, region.Max);
                    if (!region.ContainsDisc(p, _width * 0.25f)) continue;
                    var radius = new float2(_width * rng.NextFloat(0.8f, 1.65f), _width * rng.NextFloat(0.6f, 1.05f));
                    bool close = false;
                    foreach (var patch in _patches)
                        if (math.distance(p, patch.center) < (radius.y + patch.radius.y) * 1.05f) { close = true; break; }
                    if (close) continue;
                    _patches.Add((p, radius, rng.NextFloat(-math.PI, math.PI), 1 + _patches.Count % (_colors - 1)));
                }
            }

            internal int Slot(float2 p)
            {
                if (_colors == 1) return 0;
                int slot;
                if (_islands)
                {
                    slot = 0;
                    float best = 1f;
                    foreach (var patch in _patches)
                    {
                        math.sincos(patch.angle, out float s, out float c);
                        var d = p - patch.center;
                        var q = new float2(d.x * c + d.y * s, -d.x * s + d.y * c) / patch.radius;
                        // A low-frequency distortion softens the ellipses without speckled colour noise.
                        float distance = math.length(q) + 0.08f * math.sin(q.y * 3f + _phase);
                        if (distance < best) { best = distance; slot = patch.slot; }
                    }
                }
                else
                {
                    var d = p - _center;
                    float u = math.dot(d, _axis), v = math.dot(d, _cross);
                    float bend = _amplitude * math.sin(u / _length * math.PI * 2f * _wave + _phase)
                        + _amplitude * 0.22f * math.sin(u / _length * math.PI * 4f - _phase);
                    slot = (int)math.floor((v + bend) / _width);
                }
                return ((_shift + slot * _direction) % _colors + _colors) % _colors;
            }
        }

        // Scan only polygon interiors, including separate spans at a concave notch. This stays
        // bounded even for a long narrow diagonal outline with a very large bounding rectangle.
        internal static List<float2> Sample(LayerRegion region, float pitch, float margin, int limit, CancellationToken cancel)
        {
            region.PrincipalAxes(out var center, out var axis, out _, out _);
            var cross = new float2(-axis.y, axis.x);
            var ring = new float2[region.Ring.Length];
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < ring.Length; i++)
            {
                var d = region.Ring[i] - center;
                ring[i] = new float2(math.dot(d, axis), math.dot(d, cross));
                minY = math.min(minY, ring[i].y); maxY = math.max(maxY, ring[i].y);
            }
            var result = new List<float2>();
            var cuts = new List<float>();
            float dy = pitch * 0.8660254f;
            int row = 0;
            for (float y = minY + dy * 0.5f; y < maxY; y += dy, row++)
            {
                cancel.ThrowIfCancellationRequested();
                cuts.Clear();
                for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
                {
                    var a = ring[j]; var b = ring[i];
                    if ((a.y > y) != (b.y > y)) cuts.Add(a.x + (y - a.y) * (b.x - a.x) / (b.y - a.y));
                }
                cuts.Sort();
                float stagger = (row & 1) * pitch * 0.5f;
                for (int i = 0; i + 1 < cuts.Count; i += 2)
                {
                    float start = math.ceil((cuts[i] - stagger) / pitch) * pitch + stagger;
                    for (float x = start; x < cuts[i + 1]; x += pitch)
                    {
                        if ((result.Count & 127) == 0) cancel.ThrowIfCancellationRequested();
                        var p = center + axis * x + cross * y;
                        if (!region.ContainsDisc(p, margin)) continue;
                        result.Add(p);
                        if (result.Count > limit) return result;
                    }
                }
            }
            return result;
        }

        // The short horizontal span estimates a cluster's filled width. The diagonal bounding
        // radius includes empty corners, especially for elongated flowers, and leaves holes
        // when clusters are spread apart. Keep this separate from collision footprints.
        private static float FlowerRadius(SiteContext site, string id)
        {
            if (site?.Assets != null && !string.IsNullOrEmpty(id) && site.Assets.TryGetValue(id, out var info))
            {
                var span = info.BoundsMax.xz - info.BoundsMin.xz;
                float radius = math.cmin(span) * 0.5f;
                if (math.isfinite(radius) && radius > 0.05f) return radius;
            }
            return 1.1f;
        }

        internal static void Plan(LayerContext ctx, string[][] kits, string[] keys, float scale)
        {
            float radius = 0.4f;
            int colors = kits.Length;
            bool collage = ctx.S("shape") == "collage";
            var motifs = collage ? FloralMotif.Read(ctx.L("motifs")) : new List<FloralMotif>();
            var origin = FloralMotif.Origin(ctx.LayerRegion);
            bool fillGaps = !collage || ctx.B("fillGaps");
            var fills = collage ? FloralFill.Read(ctx.L("fills")) : new List<FloralFill>();
            var regions = fills.Count == 0 ? null : new FloralRegions(ctx.LayerRegion, motifs, ctx.Cancel);
            var choices = new Dictionary<int, FloralFill>();
            if (regions != null) foreach (var fill in fills) { int label = regions.At(origin + new float2(fill.X, fill.Z)); if (label > 0) choices[label] = fill; }
            if (collage && motifs.Count == 0 && !fillGaps && fills.Count == 0) { ctx.Issue(IssueSeverity.Info, "FlowerPainter.Floral.Issue.AddMotif"); return; }
            for (int i = 0; i < colors; i++)
                foreach (var id in kits[i]) radius = math.max(radius, FlowerRadius(ctx.Site, id) * scale);
            foreach (var motif in motifs)
                if (!string.IsNullOrEmpty(motif.Flower)) radius = math.max(radius, FlowerRadius(ctx.Site, motif.Flower) * scale);
            // A triangular lattice covers the ground at radius pitch / sqrt(3). Allow a little
            // extra foliage overlap without piling several clusters onto the same patch.
            // Spacing now changes density; enlarge the flowers to keep wider settings filled.
            float requestedPitch = math.max(math.clamp(ctx.F("spacing"), 0.5f, 4f) * scale / 0.65f, radius * CoveragePitch);
            radius = math.max(radius, requestedPitch / CoveragePitch);
            float pitch = math.max(requestedPitch, math.sqrt(ctx.LayerRegion.Area / (MaxObjects * 0.82f)));
            List<float2> points;
            float carpetScale, coverRadius;
            do
            {
                ctx.Cancel.ThrowIfCancellationRequested();
                carpetScale = scale * math.max(1f, pitch / requestedPitch);
                coverRadius = radius * carpetScale / scale;
                points = Sample(ctx.LayerRegion, pitch, math.max(0.1f, coverRadius * 0.65f), MaxObjects, ctx.Cancel);
                if (points.Count <= MaxObjects) break;
                pitch *= 1.08f;
            } while (true);
            if (carpetScale > scale * 1.05f)
                ctx.Issue(IssueSeverity.Info, "FlowerPainter.Floral.Issue.CarpetBudget", MaxObjects);
            var pattern = new Pattern(ctx.LayerRegion, collage ? ctx.S("gapStyle") : ctx.S("shape"), ctx.F("bandWidth"), ctx.F("rotation"), ctx.Seed, colors);
            var newPatternIslands = new Pattern(ctx.LayerRegion, "islands", ctx.F("bandWidth"), ctx.F("rotation"), ctx.Seed, colors);
            var newPatternFlow = new Pattern(ctx.LayerRegion, "flow", ctx.F("bandWidth"), ctx.F("rotation"), ctx.Seed, colors);
            var shrubs = ctx.L("shrubAssets");
            if (shrubs.Length == 0) shrubs = new[] { "GreenBushWildChild01" };
            int flowers = 0, bushes = 0;
            int gapFlowers = 0;
            var motifCounts = new int[motifs.Count];
            foreach (var p in points)
            {
                if (((flowers + bushes) & 127) == 0) ctx.Cancel.ThrowIfCancellationRequested();
                if (ctx.Input.Reserved?.Blocks(p, coverRadius * 0.7f) ?? false) continue;
                int selectedMotif = -1;
                for (int i = motifs.Count - 1; i >= 0; i--) if (motifs[i].Covers(p, origin)) { selectedMotif = i; break; }
                FloralFill fill = null;
                if (selectedMotif < 0 && regions != null) choices.TryGetValue(regions.At(p), out fill);
                if (selectedMotif < 0 && (fill?.Style == "empty" || (!fillGaps && fill == null))) continue;
                int slot = selectedMotif >= 0 ? selectedMotif % colors : pattern.Slot(p);
                if (fill?.Style == "single") slot = 0;
                else if (fill?.Style == "islands") slot = newPatternIslands.Slot(p);
                else if (fill?.Style == "flow") slot = newPatternFlow.Slot(p);
                bool bush = selectedMotif < 0 && ctx.B("shrubs") && colors > 1 && slot == colors - 1;
                // Manual flowers retain their variation and rotation when the gaps are filled or rerolled.
                uint v = selectedMotif >= 0
                    ? Hashing.Mix(Seeds.Hash(motifs[selectedMotif].Id), (int)math.round(p.x * 100), (int)math.round(p.y * 100))
                    : Hashing.Mix(ctx.Seed, (uint)(flowers + bushes + 1));
                var asset = selectedMotif < 0 && fill?.Style == "single" && !string.IsNullOrEmpty(fill.Flower) ? new AssetRef(fill.Flower, "Flowerbed01") : selectedMotif >= 0 && !string.IsNullOrEmpty(motifs[selectedMotif].Flower)
                    ? new AssetRef(motifs[selectedMotif].Flower, "Flowerbed01")
                    : AssetMix.Pick(bush ? shrubs : kits[slot], v, bush ? "GreenBushWildChild01" : "Flowerbed01");
                // Normalize selected asset sizes so a smaller species does not open holes in its colour block.
                float assetRadius = FlowerRadius(ctx.Site, asset.Id);
                float objectScale = coverRadius / math.max(0.1f, assetRadius);
                var o = ctx.Object(p, (v % 360) * math.PI / 180f, bush ? ObjectRole.Bush : ObjectRole.Planter,
                    asset, v, -1, bush ? AssetCategories.Bush : AssetCategories.Flower);
                o.Scale = objectScale;
                o.Control = selectedMotif >= 0 ? "motifs" : bush ? "shrubAssets" : keys[slot];
                if (selectedMotif >= 0) motifCounts[selectedMotif]++; else gapFlowers++;
                if (bush) bushes++; else flowers++;
            }
            if (flowers + bushes == 0) ctx.Issue(IssueSeverity.Warning, "FlowerPainter.Floral.Issue.NoRoom");
            ctx.Stat("FlowerPainter.Floral.Stat.Flowers", flowers, Keys.UnitCount);
            ctx.Stat("FlowerPainter.Floral.Stat.Shrubs", bushes, Keys.UnitCount);
            ctx.Plan.Facts["floral.flowers"] = flowers;
            ctx.Plan.Facts["floral.shrubs"] = bushes;
            ctx.Plan.Facts["floral.spacing"] = pitch;
            ctx.Plan.Facts["floral.coverRadius"] = coverRadius;
            ctx.Plan.Facts["floral.carpet"] = 1;
            ctx.Plan.Facts["floral.gapFlowers"] = gapFlowers;
            ctx.Plan.Facts["floral.motifs"] = motifs.Count;
            for (int i = 0; i < motifCounts.Length; i++) ctx.Plan.Facts["floral.motif." + i] = motifCounts[i];
        }
    }
}
