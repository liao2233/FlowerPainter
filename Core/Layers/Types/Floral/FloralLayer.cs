using System;
using System.Collections.Generic;
using FlowerPainter.Core.Geometry;
using FlowerPainter.Core.Layers.Common;
using FlowerPainter.Core.Tool;
using Unity.Mathematics;

namespace FlowerPainter.Core.Layers.Types.Floral
{
    [AreaGenerator(1, Group = "flowers")]
    public sealed class FloralLayer : LayerBase
    {
        public const string LayerId = "floral";
        protected override string Area => "Floral";
        public override string Id => LayerId;
        public override string Icon => "Media/Game/Icons/PropsPlants.svg";
        public override LayerKind Kind => LayerKind.Fill;
        public override float MinArea => 25f;
        public override float MaxArea => 250000f;
        // Plant at the requested spacing and overlap. Generic thinning would tear holes in a carpet.
        protected override float ObjectsPerHectare => 0;

        protected override void DefineControls(ControlSet c)
        {
            c.Choice("shape", "flow", ControlTier.Essential, "layout", "collage", "flow", "islands", "ring", "circle", "rosette", "spiral", "heart", "outline", "freeform").Tip();
            c.Slider("size", 85, 20, 100, 5, "%", ControlTier.Essential, "layout").Tip().When("shape=ring|circle|rosette|spiral|heart|outline|freeform");
            c.Slider("bandWidth", 4, 0.5, 16, 0.5, "m", ControlTier.Essential, "layout").Tip();
            c.Choice("colorPattern", "bands", ControlTier.Essential, "flowers", "bands", "segments", "single").Tip().When("shape=ring|circle|rosette|spiral|heart|outline|freeform");
            c.Toggle("shrubs", false, ControlTier.Essential, "planting").Tip();
            c.AssetMulti("flowerPalette", AssetCategories.Flower, ControlTier.Essential, "flowers").Tip();
            c.AssetMulti("curvePalette", AssetCategories.Flower, ControlTier.Essential, "flowers").Tip();
            c.Arrangement("motifs", AssetCategories.Flower, Array.Empty<string>(), ControlTier.Essential, "layout").Tip();
            c.Arrangement("fills", AssetCategories.Flower, Array.Empty<string>(), ControlTier.More, "layout");
            c.Choice("activeFillStyle", "single", ControlTier.More, "layout", "single", "flow", "islands", "empty");
            c.Asset("activeFillFlower", AssetCategories.Flower, ControlTier.More, "flowers");
            c.Toggle("fillGaps", false, ControlTier.Essential, "layout").Tip();
            c.Choice("gapStyle", "flow", ControlTier.More, "layout", "flow", "islands").Tip();
            c.AssetMulti("flowerAssets", AssetCategories.Flower, ControlTier.More, "flowers").Tip();
            c.AssetMulti("accentAssets", AssetCategories.Flower, ControlTier.More, "flowers").Tip();
            c.AssetMulti("detailAssets", AssetCategories.Flower, ControlTier.More, "flowers").Tip();
            c.Choice("palette", "mixed", ControlTier.More, "flowers", "mixed", "wild").Tip();
            c.Slider("spacing", 1.5, 0.5, 4, 0.1, "m", ControlTier.More, "flowers").Tip();
            c.Slider("overlap", 50, 0, 90, 5, "%", ControlTier.Essential, "flowers").Tip();
            c.Slider("flowerScale", 0.65, 0.25, 1.2, 0.05, "", ControlTier.More, "flowers").Tip();
            c.Slider("rotation", 0, 0, 355, 5, "deg", ControlTier.More, "layout").Tip();
            c.Slider("petals", 6, 3, 12, 1, "count", ControlTier.More, "layout").When("shape=rosette");
            c.Slider("turns", 2.5, 1, 5, 0.5, "count", ControlTier.More, "layout").When("shape=spiral");
            c.Choice("curve", "smooth", ControlTier.More, "layout", "smooth", "straight").When("shape=freeform");
            var points = c.Markers("controlPoints", ControlTier.More, MarkerKinds.FloralPoint).Tip().In("layout").When("shape=freeform");
            points.Options.Add(new ControlOption { Value = MarkerKinds.FloralPoint, LabelKey = "FlowerPainter.Floral.Marker.FloralPoint", TooltipKey = "FlowerPainter.Floral.controlPoints.Tooltip" });
            c.AssetMulti("shrubAssets", AssetCategories.Bush, ControlTier.More, "planting").When("shrubs=true");
            c.Slider("shrubSpacing", 3, 1.5, 8, 0.5, "m", ControlTier.More, "planting").When("shrubs=true");
            c.RandomRange("rotation", 0, 355);
            c.RandomRange("petals", 4, 8);
            foreach (var key in new[] { "shape", "size", "bandWidth", "spacing", "overlap", "flowerScale", "palette", "colorPattern", "shrubs", "shrubSpacing", "turns", "curve", "motifs", "fillGaps", "gapStyle" }) c.Fixed(key);
        }

        protected override void DefinePresets()
        {
            Preset("pattern-collage", ("shape", "collage"));
            Preset("arc-and-wave", ("shape", "collage"), ("motifs", FloralMotif.Example(1)), ("fillGaps", true), ("bandWidth", 7.0));
            Preset("wave-and-petals", ("shape", "collage"), ("motifs", FloralMotif.Example(2)), ("fillGaps", true), ("gapStyle", "islands"));
            Preset("crossing-arcs", ("shape", "collage"), ("motifs", FloralMotif.Example(3)), ("fillGaps", true), ("bandWidth", 8.0));
            Preset("flow-carpet", ("shape", "flow"), ("bandWidth", 7.0));
            Preset("island-carpet", ("shape", "islands"), ("bandWidth", 6.0));
            Preset("flower-ring", ("shape", "ring"));
            Preset("flower-disc", ("shape", "circle"), ("size", 65.0));
            Preset("petal-wreath", ("shape", "rosette"), ("petals", 6.0), ("bandWidth", 3.0));
            Preset("spiral-ribbon", ("shape", "spiral"), ("bandWidth", 2.0), ("colorPattern", "segments"));
            Preset("heart-wreath", ("shape", "heart"), ("bandWidth", 3.0));
            Preset("outline-border", ("shape", "outline"), ("bandWidth", 3.0), ("shrubs", true));
            Preset("freehand-ribbon", ("shape", "freeform"), ("bandWidth", 3.0), ("colorPattern", "segments"));
        }

        public override Readiness CheckReady(AreaInput input)
        {
            var settings = Merge(input?.Settings);
            bool empty = settings.S("shape", "collage") == "collage" && !settings.B("fillGaps", false) && FloralMotif.Read(settings.L("motifs")).Count == 0 && FloralFill.Read(settings.L("fills")).Count == 0;
            return empty ? new Readiness { Ready = false, MarkerKind = "", NextStepKey = "FlowerPainter.Floral.Issue.AddMotif", ReasonKey = "FlowerPainter.Floral.Issue.AddMotif" } : new Readiness();
        }

        public override SettingsBag Randomize(SettingsBag current, uint seed, ICollection<string> locked)
        {
            // The editor changes the layout seed. Carpet rerolls preserve every explicit control,
            // including width, density, selected plants and orientation.
            return FloralCarpet.IsCarpet(current?.S("shape", "collage")) ? current.Clone() : base.Randomize(current, seed, locked);
        }

        protected override void PlanCore(LayerContext ctx)
        {
            float scale = math.clamp(ctx.F("flowerScale"), 0.25f, 1.2f);
            var selected = new List<string>();
            foreach (var id in ctx.L("flowerPalette"))
                if (!string.IsNullOrWhiteSpace(id) && !selected.Contains(id)) selected.Add(id);
            int colors = selected.Count > 0 ? selected.Count : 3;
            var kits = new string[colors][];
            string[] keys = selected.Count > 0 ? new string[colors] : new[] { "flowerAssets", "accentAssets", "detailAssets" };
            float radius = 0.4f;
            for (int i = 0; i < colors; i++)
            {
                if (selected.Count > 0) { keys[i] = "flowerPalette"; kits[i] = new[] { selected[i] }; }
                else kits[i] = ctx.L(keys[i]);
                if (kits[i].Length == 0) kits[i] = FloralAssets.Defaults(ctx.Site, ctx.S("palette"), i);
                radius = math.max(radius, AssetMix.MaxFootprint(ctx.Site, kits[i], 1.4f) * scale);
            }
            if (FloralCarpet.IsCarpet(ctx.S("shape"))) { FloralCarpet.Plan(ctx, kits, keys, scale); return; }
            float width = math.clamp(ctx.F("bandWidth"), 0.5f, 16f);
            float pitch = FloralCarpet.Pitch(ctx.Settings);
            float margin = math.max(0.3f, ctx.Site.EdgeMargin) + radius;
            var strokes = FloralGeometry.Make(ctx.LayerRegion, ctx.Settings, ctx.Input.Markers, margin, pitch, ctx.Cancel);
            if (strokes.Count == 0) { ctx.Issue(IssueSeverity.Warning, "FlowerPainter.Floral.Issue.NoRoom"); return; }
            int rows = math.max(1, (int)math.floor(width / pitch) + 1);
            var used = new Dictionary<long, List<float2>>();
            float cell = pitch * 0.68f;
            long Key(int x, int y) => ((long)x << 32) ^ (uint)y;
            bool Take(float2 p)
            {
                int x = (int)math.floor(p.x / cell), y = (int)math.floor(p.y / cell);
                for (int a = x - 1; a <= x + 1; a++) for (int b = y - 1; b <= y + 1; b++)
                    if (used.TryGetValue(Key(a, b), out var nearby))
                        foreach (var old in nearby) if (math.distancesq(old, p) < cell * cell) return false;
                if (!used.TryGetValue(Key(x, y), out var bucket)) used[Key(x, y)] = bucket = new List<float2>();
                bucket.Add(p);
                return true;
            }
            bool Free(float2 p, float r) => ctx.LayerRegion.ContainsDisc(p, r + 0.15f) && !(ctx.Input.Reserved?.Blocks(p, r) ?? false);
            int count = 0;
            foreach (var stroke in strokes)
            {
                int nr = stroke.FilledRow ? 1 : rows;
                for (int row = 0; row < nr; row++)
                {
                    float offset = nr == 1 ? 0 : (row / (float)(nr - 1) - 0.5f) * width;
                    foreach (var sample in FloralGeometry.Samples(stroke, pitch, offset, ctx.Cancel))
                    {
                        if (!Free(sample.p, radius) || !Take(sample.p)) continue;
                        int slot = ctx.S("colorPattern") == "single" ? 0 : ctx.S("colorPattern") == "segments"
                            ? (int)math.floor(sample.along * 9f) % colors : (stroke.Band + row) % colors;
                        uint variation = Hashing.Mix(ctx.Seed, (uint)count + 1u);
                        var asset = AssetMix.Pick(kits[slot], variation, "Flowerbed01", "Flowerbed02");
                        var flower = ctx.Object(sample.p, math.atan2(sample.tangent.x, sample.tangent.y), ObjectRole.Planter,
                            asset, variation, -1, AssetCategories.Flower);
                        flower.Scale = scale;
                        flower.Control = keys[slot];
                        count++;
                    }
                }
            }
            int shrubs = 0;
            if (ctx.B("shrubs"))
            {
                var chosen = ctx.L("shrubAssets");
                if (chosen.Length == 0) chosen = new[] { "GreenBushWildChild01", "GreenBushWildTeen01" };
                float shrubScale = math.min(0.65f, scale);
                float shrubRadius = AssetMix.MaxFootprint(ctx.Site, chosen, 1f) * shrubScale;
                float shrubPitch = math.max(ctx.F("shrubSpacing"), shrubRadius * 2f + 0.1f);
                foreach (var stroke in strokes)
                {
                    // A filled disc only gets an outer rim, never a hedge between every flower row.
                    if (stroke.FilledRow && stroke.Band > 0) continue;
                    float side = stroke.Closed ? -1f : 1f;
                    float offset = side * (width * 0.5f + radius + shrubRadius + 0.4f);
                    foreach (var sample in FloralGeometry.Samples(stroke, shrubPitch, offset, ctx.Cancel))
                    {
                        if (!Free(sample.p, shrubRadius) || !Take(sample.p)) continue;
                        uint v = Hashing.Mix(ctx.Seed, (uint)shrubs + 0xFB00u);
                        ctx.Object(sample.p, 0, ObjectRole.Bush, AssetMix.Pick(chosen, v, "GreenBushWild01"), v, -1, AssetCategories.Bush).Scale = shrubScale;
                        shrubs++;
                    }
                }
            }
            if (count == 0) ctx.Issue(IssueSeverity.Warning, "FlowerPainter.Floral.Issue.NoRoom");
            ctx.Stat("FlowerPainter.Floral.Stat.Flowers", count, Keys.UnitCount);
            ctx.Stat("FlowerPainter.Floral.Stat.Shrubs", shrubs, Keys.UnitCount);
            ctx.Plan.Facts["floral.flowers"] = count;
            ctx.Plan.Facts["floral.shrubs"] = shrubs;
            ctx.Plan.Facts["floral.spacing"] = pitch;
        }
    }
}
