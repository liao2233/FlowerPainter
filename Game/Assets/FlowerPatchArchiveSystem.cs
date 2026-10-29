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
        private ArraySegment<byte> _captured = new ArraySegment<byte>(Array.Empty<byte>());
        private readonly FlowerPatchArchiveRecovery _recovery = new FlowerPatchArchiveRecovery();
        public bool HasWarning { get; private set; }
        internal int PendingSaveBytes => _captured.Count;
        internal int LastCaptureBytes { get; private set; }
        public string WarningKey => !HasWarning ? "" : _recovery.HasOpaqueData ? "FlowerPainter.Archive.Opaque" : "FlowerPainter.Archive.Partial";
        private void Warn(string message) { HasWarning=true;Mod.Log.Warn("Flower patch archive: "+message); }

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
            _captured = new ArraySegment<byte>(Array.Empty<byte>());
            var names = new HashSet<string>(StringComparer.Ordinal);
            try {
            using (var refs = _objects.ToComponentDataArray<PrefabRef>(Allocator.Temp))
                foreach (var item in refs)
                    if (_prefabs.TryGetPrefab<PrefabBase>(item.m_Prefab, out var prefab) && prefab != null &&
                        (prefab.name.StartsWith(FlowerPatchMesh.Prefix, StringComparison.Ordinal) || prefab.name.StartsWith(FlowerPatchMesh.FlagPrefix, StringComparison.Ordinal)))
                        names.Add(prefab.name);
            } catch(Exception e) when(!(e is OutOfMemoryException)) { Warn("Cannot enumerate all flower models: "+e.Message); }
            _captured=_recovery.Capture(names,(name,budget)=>_patches.ExportPatch(name,budget),MaxArchiveBytes,Warn);
            LastCaptureBytes=_captured.Count;
            Mod.Log.Info("Flower patch save archive: " + names.Count + " referenced models, " + _captured.Count + " bytes");
        }

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            try {
                writer.Write(_captured.Count);
                using var bytes = new NativeArray<byte>(_captured.Count, Allocator.Temp);
                NativeArray<byte>.Copy(_captured.Array,_captured.Offset,bytes,0,_captured.Count);
                writer.Write(bytes);
            } finally { _captured=new ArraySegment<byte>(Array.Empty<byte>()); }
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            SetDefaults(default);
            reader.Read(out int length);
            if (length < 0 || length > MaxArchiveBytes) throw new System.IO.InvalidDataException("Invalid flower patch save archive length");
            using var bytes = new NativeArray<byte>(length, Allocator.Temp);
            reader.Read(bytes);
            _data = bytes.ToArray();
        }

        public void SetDefaults(Context context) {
            _data=Array.Empty<byte>();_captured=new ArraySegment<byte>(Array.Empty<byte>());
            _recovery.Clear();HasWarning=false;
        }

        private void Restore()
        {
            if (_data.Length == 0) return; // Saves made before the archive existed.
            bool created = false;
            try {
                _recovery.Restore(_data,entry=>created|=_patches.RestorePatch(entry),()=>{if(created)ResolveRestoredPrefabs();},Warn);
            } finally { _data=Array.Empty<byte>(); }
        }

        private void ResolveRestoredPrefabs()
        {
            // PrefabSystem.Deserialize has already recorded unknown IDs. Register their newly
            // restored entities under the same loaded indices before ResolvePrefabsSystem runs.
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(PrefabSystem);
            var missing = type.GetField("m_LoadedObsoleteIDs", flags)?.GetValue(_prefabs) as Dictionary<int, PrefabID>;
            var loaded = type.GetField("m_LoadedIndexData", flags)?.GetValue(_prefabs) as IList;
            var itemType = type.GetNestedType("LoadedIndexData", BindingFlags.NonPublic);
            var entityField = itemType?.GetField("m_Entity");
            var indexField = itemType?.GetField("m_Index");
            if(missing==null || loaded==null || itemType==null || entityField==null || indexField==null)
                throw new MissingMemberException("Game prefab loading fields changed; cannot reconnect restored flower models");
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
