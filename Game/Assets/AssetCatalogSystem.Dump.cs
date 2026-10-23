// FlowerPainter — asset survey dump (GPL-3.0).
// With detailed diagnostics enabled, catalog scans and icon changes queue a background survey write:
// { "version", "written", "categories": { category: [ {id, displayName, tags, auto, vanilla, bounds,
// icon, iconSource}, … ] } } so the content team can design new park types from real assets.
// See docs/ASSET-SURVEY-HOWTO.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using FlowerPainter.Game.Settings;

namespace FlowerPainter.Game.Assets
{
    public sealed partial class AssetCatalogSystem
    {
        public const string DumpFileName = "catalog-dump.json";
        /// <summary>Setting hook: opt-in survey dump; background writes are coalesced at most every 10 s.</summary>
        public static bool WriteSurveyDump = false;
        private const float DumpMinInterval = 10f;
        private bool _dumpDirty;
        private float _lastDumpTime = -1000f;
        private Task _dumpTask;

        private void MaybeWriteDump(bool force)
        {
            if (_dumpTask != null)
            {
                if (!_dumpTask.IsCompleted) { if (force) _dumpDirty = true; return; }
                if (_dumpTask.IsFaulted) Mod.Log.Warn("FlowerPainter asset survey write failed: " + _dumpTask.Exception.GetBaseException().Message);
                _dumpTask = null;
            }
            if (!WriteSurveyDump || !_ready) return;
            if (force) _dumpDirty = true;
            if (!_dumpDirty) return;
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (now - _lastDumpTime < DumpMinInterval) return;
            _dumpDirty = false;
            _lastDumpTime = now;
            try
            {
                var path = Path.Combine(ModPaths.DataFolder, DumpFileName);
                var categories = new Dictionary<string, List<CatalogItem>>(StringComparer.Ordinal);
                foreach (var kv in _byCategory)
                {
                    var items = new List<CatalogItem>(kv.Value.Count);
                    foreach (var it in kv.Value) items.Add(new CatalogItem { Id = it.Id, DisplayName = it.DisplayName,
                        Icon = it.Icon, Info = it.Info, Tags = it.Tags == null ? null : (string[])it.Tags.Clone() });
                    categories[kv.Key] = items;
                }
                var sources = new Dictionary<string, string>(_iconSource, StringComparer.Ordinal);
                var pools = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                foreach (var kv in _autoPools) pools[kv.Key] = new List<string>(kv.Value);
                var palettes = new List<string>(_palettes);
                int version = _version;
                _dumpTask = Task.Run(() => ModPaths.WriteAllTextAtomic(path,
                    BuildDump(categories, sources, pools, palettes, version).ToString(Formatting.Indented)));
            }
            catch (Exception e)
            {
                Mod.Log.Warn("FlowerPainter could not write the asset survey dump: " + e.Message);
            }
        }

        private static JObject BuildDump(Dictionary<string, List<CatalogItem>> _byCategory,
            Dictionary<string, string> iconSources, Dictionary<string, List<string>> _autoPools, List<string> _palettes, int _version)
        {
            var categories = new JObject();
            var keys = new List<string>(_byCategory.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (var c in keys)
            {
                var arr = new JArray();
                foreach (var it in _byCategory[c])
                {
                    var tags = it.Tags ?? Array.Empty<string>();
                    var o = new JObject
                    {
                        ["id"] = it.Id,
                        ["displayName"] = it.DisplayName,
                        ["tags"] = new JArray(tags),
                        ["auto"] = Array.IndexOf(tags, "auto:" + c) >= 0,
                        ["vanilla"] = Array.IndexOf(tags, "source:vanilla") >= 0,
                        ["icon"] = it.Icon ?? "",
                        ["iconSource"] = iconSources.TryGetValue(it.Id, out var source) ? source : "none",
                    };
                    var i = it.Info;
                    if ((i.BoundsMax.x > i.BoundsMin.x || i.BoundsMax.z > i.BoundsMin.z))
                        o["bounds"] = new JObject
                        {
                            ["min"] = new JArray(Round(i.BoundsMin.x), Round(i.BoundsMin.y), Round(i.BoundsMin.z)),
                            ["max"] = new JArray(Round(i.BoundsMax.x), Round(i.BoundsMax.y), Round(i.BoundsMax.z)),
                            ["size"] = new JArray(Round(i.BoundsMax.x - i.BoundsMin.x), Round(i.BoundsMax.y - i.BoundsMin.y), Round(i.BoundsMax.z - i.BoundsMin.z)),
                        };
                    if (i.NetWidth > 0f) o["netWidth"] = Round(i.NetWidth);
                    if (i.Cost > 0f) o["cost"] = Round(i.Cost);
                    arr.Add(o);
                }
                categories[c] = arr;
            }
            var pools = new JObject();
            foreach (var kv in _autoPools) pools[kv.Key] = new JArray(kv.Value);
            return new JObject
            {
                ["version"] = _version,
                ["written"] = DateTime.UtcNow.ToString("o"),
                ["counts"] = new JObject(BuildCounts(_byCategory)),
                ["automaticPools"] = pools,
                ["palettes"] = new JArray(_palettes),
                ["categories"] = categories,
            };
        }

        private static IEnumerable<JProperty> BuildCounts(Dictionary<string, List<CatalogItem>> _byCategory)
        {
            var keys = new List<string>(_byCategory.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (var c in keys) yield return new JProperty(c, _byCategory[c].Count);
        }

        private static double Round(float v) => Math.Round(v, 2);
    }
}
