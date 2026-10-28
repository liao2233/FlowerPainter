using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Colossal.Serialization.Entities;
using FlowerPainter.Core.Layers.Types.Floral;
using Game;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Scripting;

namespace FlowerPainter.Game.Assets
{
    // Save generated meshes with the city, then restore them before the game's prefab resolution.
    public sealed partial class FlowerPatchArchiveSystem : GameSystemBase, IDefaultSerializable
    {
        private EntityQuery _objects;
        private PrefabSystem _prefabs;
        private UpdateSystem _updates;
        private FlowerPatchPrefabSystem _patches;
        private const int MaxArchiveBytes = 512 * 1024 * 1024;
        private byte[] _data = Array.Empty<byte>();

        [Preserve] protected override void OnCreate()
        {
            base.OnCreate();
            _objects = GetEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<PrefabRef>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() } });
            _prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();
            _patches = World.GetOrCreateSystemManaged<FlowerPatchPrefabSystem>();
            _updates = World.GetOrCreateSystemManaged<UpdateSystem>();
        }

        [Preserve] protected override void OnUpdate()
        {
            if (_updates.currentPhase == SystemUpdatePhase.Serialize) Capture();
            else if (_updates.currentPhase == SystemUpdatePhase.Deserialize) Restore();
        }

        private void Capture()
        {
            // Do not accidentally reuse another city's archive if capture fails.
            _data = Array.Empty<byte>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            using (var refs = _objects.ToComponentDataArray<PrefabRef>(Allocator.Temp))
                foreach (var item in refs)
                    if (_prefabs.TryGetPrefab<PrefabBase>(item.m_Prefab, out var prefab) && prefab != null &&
                        (prefab.name.StartsWith(FlowerPatchMesh.Prefix, StringComparison.Ordinal) || prefab.name.StartsWith(FlowerPatchMesh.FlagPrefix, StringComparison.Ordinal)))
                        names.Add(prefab.name);
            var entries = new List<FlowerPatchArchive.Entry>(names.Count);
            foreach (var name in names) entries.Add(_patches.ExportPatch(name));
            var data = FlowerPatchArchive.Encode(entries);
            if (data.Length > MaxArchiveBytes) throw new System.IO.InvalidDataException("Flower patch save archive exceeds the supported size");
            _data = data;
            Mod.Log.Info("Flower patch save archive: " + entries.Count + " models, " + _data.Length + " bytes");
        }

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(_data.Length);
            using var bytes = new NativeArray<byte>(_data, Allocator.Temp);
            writer.Write(bytes);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out int length);
            if (length < 0 || length > MaxArchiveBytes) throw new System.IO.InvalidDataException("Invalid flower patch save archive length");
            using var bytes = new NativeArray<byte>(length, Allocator.Temp);
            reader.Read(bytes);
            _data = bytes.ToArray();
        }

        public void SetDefaults(Context context) => _data = Array.Empty<byte>();

        private void Restore()
        {
            if (_data.Length == 0) return; // Saves made before the archive existed.
            var entries = FlowerPatchArchive.Decode(_data);
            _data = Array.Empty<byte>();
            Mod.Log.Info("Flower patch save archive restoring: " + entries.Count + " models");
            if (entries.Count == 0) return;
            bool created = false;
            foreach (var entry in entries) created |= _patches.RestorePatch(entry);
            if (!created) return;
            // PrefabSystem.Deserialize has already recorded unknown IDs. Register their newly
            // restored entities under the same loaded indices before ResolvePrefabsSystem runs.
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(PrefabSystem);
            var missing = (Dictionary<int, PrefabID>)type.GetField("m_LoadedObsoleteIDs", flags).GetValue(_prefabs);
            var loaded = (IList)type.GetField("m_LoadedIndexData", flags).GetValue(_prefabs);
            var itemType = type.GetNestedType("LoadedIndexData", BindingFlags.NonPublic);
            var entityField = itemType.GetField("m_Entity");
            var indexField = itemType.GetField("m_Index");
            foreach (var pair in missing)
            {
                if (!_prefabs.TryGetPrefab(pair.Value, out var prefab)) continue;
                var name = pair.Value.GetName();
                if (!name.StartsWith(FlowerPatchMesh.Prefix, StringComparison.Ordinal) && !name.StartsWith(FlowerPatchMesh.FlagPrefix, StringComparison.Ordinal)) continue;
                var item = Activator.CreateInstance(itemType);
                entityField.SetValue(item, _prefabs.GetEntity(prefab));
                indexField.SetValue(item, pair.Key);
                loaded.Add(item);
            }
            _prefabs.Update();
        }
    }
}
