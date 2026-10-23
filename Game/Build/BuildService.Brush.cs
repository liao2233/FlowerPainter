using System;
using System.Collections.Generic;
using Game.Common;
using Game.Prefabs;
using FlowerPainter.Core;
using FlowerPainter.Core.Build;
using FlowerPainter.Core.Geometry;
using FlowerPainter.Game.Groups;
using Unity.Entities;
using Unity.Mathematics;

namespace FlowerPainter.Game.Build
{
    public sealed partial class BuildService
    {
        private Entity _brushRecord;
        private GroupRemovalSystem.BrushRemoval _brushRemoval;
        private readonly Dictionary<Entity, global::Game.Objects.Transform> _brushSurvivors = new Dictionary<Entity, global::Game.Objects.Transform>();
        private readonly HashSet<Entity> _brushVisible = new HashSet<Entity>();
        private PlanIndex _brushOriginalNets;

        private bool BeginBrushEdit()
        {
            if (!_registry.TryGetRecordEntity(_request.BrushGroup, out var record)
                || EntityManager.HasComponent<FlowerPainterRemoval>(record)
                || EntityManager.HasComponent<FlowerPainterUnderConstruction>(record)) return false;
            // Never replace a surviving piece with a category fallback when its original asset is unavailable.
            if (_request.BrushErase)
                foreach (var w in _work.All())
                {
                    if (w.Kind == WorkKind.Terraform) continue;
                    var prefab = PrefabOf(w);
                    if (prefab == Entity.Null || _catalog.NameOf(prefab) != w.Asset.Id) return false;
                }
            _brushRecord = record;
            EntityManager.AddComponent<FlowerPainterUnderConstruction>(_brushRecord);
            var removing = new HashSet<Entity>(_request.BrushRemove);
            var members = new List<Entity>();
            _registry.GetMembers(_request.BrushGroup, members);
            _brushOriginalNets = new PlanIndex();
            foreach (var e in members)
            {
                if (EntityManager.HasComponent<global::Game.Net.Curve>(e) && EntityManager.HasComponent<PrefabRef>(e))
                {
                    var c = EntityManager.GetComponentData<global::Game.Net.Curve>(e).m_Bezier;
                    _brushOriginalNets.AddCurve(GroupRemovalSystem.Id(e), PrefabId(EntityManager.GetComponentData<PrefabRef>(e).m_Prefab), new Cubic(c.a,c.b,c.c,c.d));
                }
                if (!removing.Contains(e) && EntityManager.HasComponent<global::Game.Objects.Transform>(e))
                {
                    _brushSurvivors[e] = EntityManager.GetComponentData<global::Game.Objects.Transform>(e);
                    if (!EntityManager.HasComponent<Overridden>(e)) _brushVisible.Add(e);
                }
            }
            if (_request.BrushRemove.Count > 0)
            {
                _brushRemoval = World.GetOrCreateSystemManaged<GroupRemovalSystem>().RemoveBrushParts(_request.BrushGroup, _request.BrushRemove);
                _replacementStarted = true;
            }
            return true;
        }

        private void ReleaseBrushEdit()
        {
            if (_brushRecord != Entity.Null && EntityManager.Exists(_brushRecord)
                && EntityManager.HasComponent<FlowerPainterUnderConstruction>(_brushRecord)) EntityManager.RemoveComponent<FlowerPainterUnderConstruction>(_brushRecord);
            _brushRecord = Entity.Null;
            _brushRemoval = null;
            _brushSurvivors.Clear();
            _brushVisible.Clear();
            _brushOriginalNets = null;
        }

        // CourseSplit may create a continuation of an existing group path outside the repaired
        // subcurve. Recognize it against the source group, never against an unrelated city road.
        private bool MatchBrushContinuation(Entity e, Entity prefab, out long key)
        {
            key = 0;
            if (_brushOriginalNets == null || !EntityManager.HasComponent<global::Game.Net.Curve>(e)) return false;
            var c = EntityManager.GetComponentData<global::Game.Net.Curve>(e).m_Bezier;
            long pid = PrefabId(prefab);
            for (int i=0;i<=4;i++)
                if (!_brushOriginalNets.TryMatch(Colossal.Mathematics.MathUtils.Position(c,i/4f).xz,pid,1.5f,out _,out _)) return false;
            foreach (var w in _work.Nets)
                if (PrefabOf(w)==prefab) { key=PlanWork.Key(w); return true; }
            return false;
        }

        private void FinishBrushEdit()
        {
            int target = _request.BrushGroup;
            var rec = _registry.GetRecord(target)?.Clone();
            if (rec == null || !EntityManager.Exists(_brushRecord)) throw new InvalidOperationException("Brush target disappeared");
            var patch = _registry.GetRecord(_groupId);
            // Rebuilding a clipped path makes vanilla re-fit nearby props to the ground. Keep the
            // exact transforms of members outside the stroke, including their original height/tilt.
            foreach (var kv in _brushSurvivors)
                if (EntityManager.Exists(kv.Key) && !EntityManager.HasComponent<Deleted>(kv.Key))
                {
                    EntityManager.SetComponentData(kv.Key, kv.Value);
                    if (_brushVisible.Contains(kv.Key) && EntityManager.HasComponent<Overridden>(kv.Key))
                    {
                        EntityManager.RemoveComponent<Overridden>(kv.Key);
                        if (EntityManager.HasComponent<PrefabRef>(kv.Key)) RestoreSearchMask(kv.Key, EntityManager.GetComponentData<PrefabRef>(kv.Key).m_Prefab);
                    }
                    if (!EntityManager.HasComponent<BatchesUpdated>(kv.Key)) EntityManager.AddComponent<BatchesUpdated>(kv.Key);
                }
            var members = new List<Entity>();
            _registry.GetMembers(_groupId, members);
            int added = members.Count;
            var ownerPrefabs = World.GetOrCreateSystemManaged<OwnerAreaPrefabSystem>();
            foreach (var e in members)
            {
                var tag = EntityManager.GetComponentData<FlowerPainterMember>(e);
                tag.GroupId = target;
                if (EntityManager.HasComponent<PrefabRef>(e))
                {
                    var prefab = EntityManager.GetComponentData<PrefabRef>(e).m_Prefab;
                    if (ownerPrefabs.IsOwnerPrefab(prefab)) { tag.Role = MemberRole.OwnerArea; tag.Kind = MemberKind.Auxiliary; }
                    else if (ownerPrefabs.IsBlockerPrefab(prefab)) tag.Role = MemberRole.ZoneBlocker;
                }
                EntityManager.SetComponentData(e, tag);
            }
            rec.BrushRegions = _request.BrushRegions ?? rec.Footprint();
            rec.Flags |= GroupFlags.Modified;
            if (patch != null)
            {
                rec.Cost += patch.Cost;
                if (!_request.BrushErase) rec.Upkeep += patch.Upkeep;
                foreach (var name in patch.PrefabNames) if (!rec.PrefabNames.Contains(name)) rec.PrefabNames.Add(name);
            }
            if (rec.BrushRegions.Count > 0)
            {
                var largest = rec.BrushRegions[0];
                foreach (var r in rec.BrushRegions) if (r.Area > largest.Area) largest = r;
                rec.Area = new AreaShape { Ring = Poly.To3D(largest.Outer, Height), Source = AreaSource.Painted };
            }
            // Retire only the temporary patch record. Its members now belong to the original group.
            if (_record != Entity.Null && EntityManager.Exists(_record)) EntityManager.AddComponent<Deleted>(_record);
            _registry.MarkDirty();
            _registry.WriteRecord(_brushRecord, rec);
            var g = EntityManager.GetComponentData<FlowerPainterGroup>(_brushRecord);
            members.Clear(); _registry.GetMembers(target, members);
            g.Owner = Entity.Null;
            foreach (var e in members)
                if (EntityManager.GetComponentData<FlowerPainterMember>(e).Role == MemberRole.OwnerArea) { g.Owner = e; break; }
            g.BuiltMembers = _registry.CountMembers(target, false);
            EntityManager.SetComponentData(_brushRecord, g);
            ReleaseBrushEdit();
            _registry.MarkDirty();
            if (rec.BrushRegions.Count == 0) _registry.RemoveInternal(target);
            Mod.Log.Info($"FlowerPainter brush edit: group {target}, erase={_request.BrushErase}, removed={_request.BrushRemove.Count}, added={added}, regions={rec.BrushRegions.Count}");
            _groupId = target;
        }
    }
}
