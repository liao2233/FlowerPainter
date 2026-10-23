// A feature-only park adapter: access gates are derived from the parent streets, never requested from the player.
using System;
using System.Collections.Generic;
using System.Threading;
using FlowerPainter.Core.Geometry;
using FlowerPainter.Core.Layers.Common;
using FlowerPainter.Core.Layers.Park;
using Unity.Mathematics;

namespace FlowerPainter.Core.Layers.Roads
{
    [AreaGenerator(401, Group = "roads", Hidden = true)]
    public sealed class NeighborhoodGardenLayer : LayerBase
    {
        public const string LayerId = "neighborhoodGarden";
        protected override string Area => "Neighborhood";
        public override string Id => LayerId;
        public override string Icon => "Media/Game/Icons/ParksAndRecreation.svg";
        public override LayerKind Kind => LayerKind.Fill;
        public override float MinArea => 100f;
        public override string[] Accepts => new[] { NeighborhoodLayout.Target };
        public override string[] Emits => new[] { RegionTags.Green, RegionTags.Core, RegionTags.Edge };

        protected override void DefineControls(ControlSet c)
        {
            c.Choice("style", "pocket", ControlTier.Essential, "layout", "pocket", "natural", "formal").Tip();
            c.Slider("treeDensity", 35, 0, 100, 5, "perHa", ControlTier.Essential, "planting").Tip();
            c.Slider("furniture", 3, 0, 8, 0.5, "per100m", ControlTier.Essential, "furniture").Tip();
            c.Toggle("lamps", true, ControlTier.More, "furniture");
            c.Toggle("bins", true, ControlTier.More, "furniture");
            c.AssetMulti("treeSpecies", AssetCategories.Tree, ControlTier.More, "planting");
        }

        protected override void PlanCore(LayerContext ctx)
        {
            var markers = Gates(ctx.Input, ctx.LayerRegion);
            if (markers.Count == 0) { ctx.Issue(IssueSeverity.Warning, "FlowerPainter.Neighborhood.Issue.NoAccess"); return; }
            var park = new ParkLayer();
            var settings = park.Defaults();
            foreach (var key in new[] { "style", "treeDensity", "furniture", "lamps", "bins", "treeSpecies" })
                if (ctx.Settings.Values.TryGetValue(key, out var value)) settings.Values[key] = value;
            settings.Values["paths"] = ctx.LayerRegion.Area > 1200f ? "loop" : "minimal";
            settings.Values["pathWidth"] = "narrow";
            settings.Values["belt"] = false;
            settings.Values["groundSurface"] = false;
            settings.Values["naturalGround"] = false;
            settings.Values["bushDensity"] = 45.0;
            var input = new AreaInput
            {
                Shape = ctx.Input.Shape, Site = ctx.Site, Settings = settings, Seed = ctx.Seed,
                Markers = markers, Reserved = ctx.Input.Reserved, RegionTags = ctx.Input.RegionTags,
                ChildLayers = Array.Empty<string>(), ChildTargets = Array.Empty<string>(),
            };
            var result = park.Plan(input, ctx.Cancel);
            ctx.Plan.Nets.AddRange(result.Nets);
            ctx.Plan.Objects.AddRange(result.Objects);
            ctx.Plan.Surfaces.AddRange(result.Surfaces);
            ctx.Plan.Areas.AddRange(result.Areas);
            ctx.Plan.Regions.AddRange(result.Regions);
            ctx.Plan.Stats.AddRange(result.Stats);
            ctx.Plan.Issues.AddRange(result.Issues);
            // Park-local grading is handled once by the composer's shared SiteHarmony pass.
        }

        internal static List<EdgeMarker> Gates(AreaInput input, LayerRegion region)
        {
            var candidates = new List<(float gap, int edge, float2 p)>();
            for (int i = 0; i < region.Ring.Length; i++)
            {
                var a = region.Ring[i]; var b = region.Ring[(i + 1) % region.Ring.Length];
                if (math.distance(a, b) < 4f) continue;
                var p = (a + b) * 0.5f;
                float gap = float.MaxValue;
                foreach (var road in input.Reserved?.Corridors ?? new List<ReservedSpace.Corridor>())
                {
                    if (!Enum.TryParse(road.Tag, out NetRole role) || role == NetRole.Pathway || role == NetRole.PathwayWide || role == NetRole.Fence) continue;
                    gap = math.min(gap, math.abs(math.sqrt(ReservedSpace.DistSqToSegment(p, road.A, road.B)) - road.HalfWidth));
                }
                foreach (var road in input.Site?.NearbyNets ?? new List<NearbyNet>())
                {
                    if (road.Category != NetCategory.Road) continue;
                    for (int s = 1; s <= 12; s++)
                    {
                        var c = Composer.Bezier(road.A, road.B, road.C, road.D, (s - 1) / 12f).xz;
                        var d = Composer.Bezier(road.A, road.B, road.C, road.D, s / 12f).xz;
                        gap = math.min(gap, math.abs(math.sqrt(ReservedSpace.DistSqToSegment(p, c, d)) - road.Width * 0.5f));
                    }
                }
                if (gap <= 3.5f) candidates.Add((gap, i, p));
            }
            candidates.Sort((a, b) => a.gap != b.gap ? a.gap.CompareTo(b.gap) : a.edge.CompareTo(b.edge));
            var result = new List<EdgeMarker>();
            foreach (var c in candidates)
            {
                bool apart = true;
                foreach (var m in result) if (math.distance(m.Position.xz, c.p) < 20f) apart = false;
                if (!apart) continue;
                float h = input.Site?.HeightAt != null ? input.Site.HeightAt(c.p) : 0f;
                result.Add(new EdgeMarker { Kind = EntranceKinds.Pedestrian, Position = new float3(c.p.x, h, c.p.y), RingSegment = c.edge, RingT = 0.5f });
                if (result.Count == 2) break;
            }
            return result;
        }
    }
}
