using System;
using System.Collections.Generic;
using FlowerPainter.Core.Build;

namespace FlowerPainter.Core
{
    public static partial class AssetCategories
    {
        public const string Flower = "flower";
    }
}

namespace FlowerPainter.Core.Layers.Types.Floral
{
    // Manual selection includes cultivated and potted flowers too; automatic carpets avoid containers.
    public sealed class FloralCategoryRules : ICategoryRules
    {
        public int Priority => 80;

        public static bool IsFlower(string lower) => !string.IsNullOrEmpty(lower)
            && !AssetHeuristics.ContainsAny(lower, "dead", "stump", "tree", "decal", "sign", "surface", "billboard", "astand", "pylon", "advert", "logo", "shop")
            && AssetHeuristics.ContainsAny(lower, "flower", "bluebell", "poppy", "lilac", "bougainvillea", "lavender", "tulip", "daisy", "petunia",
                "vibrantbush", "rose", "dahlia", "peony", "chrysanthemum", "hydrangea", "fuchsia", "marigold", "begonia", "pansy", "zinnia", "iris", "lily", "lilies", "salvia");

        public bool Classify(string lower, AssetTraits traits, List<string> categories)
        {
            // Flower/shrub prefabs can carry TreeData for growth stages; their name distinguishes them from trees.
            const AssetTraits excluded = AssetTraits.Building | AssetTraits.BuildingPart | AssetTraits.Surface
                | AssetTraits.Net | AssetTraits.Vehicle | AssetTraits.DecalMesh | AssetTraits.WallOrHanging;
            if (IsFlower(lower) && (traits & AssetTraits.HasMesh) != 0 && (traits & excluded) == 0) categories.Add(AssetCategories.Flower);
            return false;
        }

        public bool? AutoEligible(string category, string lower, AssetTraits traits) =>
            category == AssetCategories.Flower ? IsFlower(lower)
                && !AssetHeuristics.ContainsAny(lower, "pot", "planter", "box", "raised", "hanging", "random") : (bool?)null;
    }

    internal static class FloralAssets
    {
        // The first installed item is used. Every slot has vanilla fallbacks, so asset packs are optional.
        private static readonly string[][] Mixed =
        {
            new[] { "VFP PoppyBushWild", "VFP BougainvilleaWild", "Flowerbed01", "FlowerBushWild01" },
            new[] { "VFP BluebellWild", "VFP LilacWild", "Flowerbed02", "FlowerBushWild02" },
            new[] { "VFP ChannelIslandsPoppyWild", "VFP LilacWild", "FlowerBushWildChild02", "Flowerbed01" },
        };
        private static readonly string[][] Wild =
        {
            new[] { "VFP BougainvilleaWild", "FlowerBushWild01", "Flowerbed01" },
            new[] { "VFP LilacWild", "FlowerBushWild02", "Flowerbed02" },
            new[] { "VFP BluebellWild", "FlowerBushWildChild02", "Flowerbed01" },
        };
        internal static string[] Defaults(SiteContext site, string palette, int slot)
        {
            var choices = (palette == "wild" ? Wild : Mixed)[slot % 3];
            if (site?.Assets != null)
                foreach (var id in choices) if (site.Assets.ContainsKey(id)) return new[] { id };
            return new[] { choices[choices.Length - 2] };
        }
    }
}
