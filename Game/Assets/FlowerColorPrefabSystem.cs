// FlowerPainter — separate flower colour props, sharing the original geometry. GPL-3.0.
using System;
using System.Collections.Generic;
using Game;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;

namespace FlowerPainter.Game.Assets
{
    // Register before prefab initialization, including before saved objects resolve their prefab IDs.
    public sealed partial class FlowerColorPrefabSystem : GameSystemBase
    {
        public const string Prefix = "FlowerPainter Flower ";
        private PrefabSystem _prefabs;
        private readonly HashSet<string> _created = new HashSet<string>();
        private static readonly string[] Sources = { "Flowerbed01", "Flowerbed02" };
        private static readonly (string name, string hex)[] Colors = {
            ("Red", "F04452"), ("Orange", "FF9238"), ("Yellow", "FFE34D"),
            ("Pink", "FFA3CF"), ("Magenta", "D741B3"), ("Purple", "9259DE"),
            ("Blue", "538EF0"), ("White", "FFF6E6")
        };

        [Preserve] protected override void OnCreate()
        {
            base.OnCreate();
            _prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();
        }

        [Preserve] protected override void OnUpdate()
        {
            foreach (var source in Sources)
            {
                if (_created.Contains(source)) continue;
                if (!_prefabs.TryGetPrefab(new PrefabID(nameof(StaticObjectPrefab), source), out var found)
                    || !(found is StaticObjectPrefab original)) continue;
                foreach (var color in Colors) Create(original, color.name, color.hex);
                _created.Add(source);
                World.GetOrCreateSystemManaged<AssetCatalogSystem>().RequestRescan();
            }
        }

        private void Create(StaticObjectPrefab original, string colorName, string hex)
        {
            var name = Prefix + colorName + " " + original.name;
            if (_prefabs.TryGetPrefab(new PrefabID(original.GetType().Name, name), out _)) return;
            ColorUtility.TryParseHtmlString("#" + hex, out var tint);
            var meshes = new ObjectMeshInfo[original.m_Meshes.Length];
            int changed = 0;
            for (int i = 0; i < meshes.Length; i++)
            {
                var mi = original.m_Meshes[i];
                var mesh = mi.m_Mesh;
                if (mesh is RenderPrefab rp && rp.GetComponent<ColorProperties>() is ColorProperties colors)
                {
                    var copy = ScriptableObject.CreateInstance<RenderPrefab>();
                    copy.name = name + " mesh " + i;
                    copy.geometryAsset = rp.geometryAsset;
                    copy.surfaceAssets = rp.surfaceAssets;
                    copy.bounds = rp.bounds; copy.surfaceArea = rp.surfaceArea;
                    copy.meshCount = rp.meshCount; copy.vertexCount = rp.vertexCount; copy.indexCount = rp.indexCount;
                    copy.isImpostor = rp.isImpostor; copy.manualVTRequired = rp.manualVTRequired;
                    foreach (var component in rp.components)
                        if (component != null && !(component is ObsoleteIdentifiers)) copy.AddComponentFrom(component);
                    var target = copy.GetComponent<ColorProperties>();
                    target.m_ColorVariations = new List<ColorProperties.VariationSet>();
                    target.m_VariationRanges = int3.zero;
                    target.m_VariationGroups = null;
                    foreach (var variation in colors.m_ColorVariations)
                    {
                        var channels = (Color[])variation.m_Colors.Clone();
                        // Both vanilla Flowerbed models use channel 0 for leaves, 1 for
                        // blossoms and 2 for soil/edging. Green seasonal blossom variants
                        // must also get the selected colour; foliage and ground stay intact.
                        channels[1] = new Color(tint.r, tint.g, tint.b, channels[1].a);
                        changed++;
                        target.m_ColorVariations.Add(new ColorProperties.VariationSet { m_Colors = channels });
                    }
                    if (!AssetCatalogSystem.AddOwnPrefab(_prefabs, copy)) throw new InvalidOperationException("Cannot register " + copy.name);
                    mesh = copy;
                }
                meshes[i] = new ObjectMeshInfo { m_Mesh = mesh, m_Position = mi.m_Position, m_Rotation = mi.m_Rotation, m_RequireState = mi.m_RequireState };
            }
            if (changed == 0) throw new InvalidOperationException("No blossom colour channels on " + original.name);
            var clone = (StaticObjectPrefab)ScriptableObject.CreateInstance(original.GetType());
            clone.name = name; clone.m_Meshes = meshes; clone.m_Circular = original.m_Circular;
            foreach (var component in original.components)
                if (component != null && !(component is UIObject) && !(component is ObsoleteIdentifiers)) clone.AddComponentFrom(component);
            if (!AssetCatalogSystem.AddOwnPrefab(_prefabs, clone)) throw new InvalidOperationException("Cannot register " + name);
            Mod.Log.Info("Flower colour prop: " + name + ", blossom channels=" + changed);
        }
    }
}
