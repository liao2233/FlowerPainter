using System.Collections.Generic;
using Game.Common;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;

namespace FlowerPainter.Game.Build
{
    public sealed partial class BuildService
    {
        private const TempFlags PreviewOutlineFlags = TempFlags.Create | TempFlags.Delete | TempFlags.Select | TempFlags.Modify | TempFlags.Replace | TempFlags.Parent | TempFlags.SubDetail;
        private readonly Dictionary<Entity, TempFlags> _flowerPreviewFlags = new Dictionary<Entity, TempFlags>();

        // BatchInstanceSystem adds the cyan outline from these Temp flags. Keep Temp itself:
        // previews remain uncommitted, unsaved and removable by the game's normal Clear pass.
        private void CleanFlowerPreview()
        {
            using var entities = _tempQuery.ToEntityArray(Allocator.Temp);
            foreach(var e in entities) {
                if (!EntityManager.HasComponent<global::Game.Objects.Transform>(e) || !IsOwnCandidate(e,out var temp) || !TryMatch(e,out _,out _)) continue;
                var flags=temp.m_Flags & PreviewOutlineFlags;
                if(flags==0) continue;
                if(!_flowerPreviewFlags.ContainsKey(e)) _flowerPreviewFlags[e]=flags;
                temp.m_Flags &= ~PreviewOutlineFlags; EntityManager.SetComponentData(e,temp);
                if(!EntityManager.HasComponent<BatchesUpdated>(e)) EntityManager.AddComponent<BatchesUpdated>(e);
            }
        }
        private void RestoreFlowerPreviewFlags()
        {
            foreach(var pair in _flowerPreviewFlags) if(EntityManager.Exists(pair.Key) && EntityManager.HasComponent<Temp>(pair.Key)) {
                var temp=EntityManager.GetComponentData<Temp>(pair.Key); temp.m_Flags |= pair.Value;
                EntityManager.SetComponentData(pair.Key,temp);
                if(!EntityManager.HasComponent<BatchesUpdated>(pair.Key)) EntityManager.AddComponent<BatchesUpdated>(pair.Key);
            }
            _flowerPreviewFlags.Clear();
        }
    }
}
