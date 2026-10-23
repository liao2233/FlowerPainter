using System;
using System.Collections.Generic;
using Game.City;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using FlowerPainter.Core;
using FlowerPainter.Core.Build;
using FlowerPainter.Game.Groups;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace FlowerPainter.Game.Build
{
    public sealed partial class BuildService
    {
        private BuildRequest _recoveryRequest;
        private GroupRecord _recoveryRecord;
        private bool _recovering, _replacementStarted;
        private bool _recoveryPermanentRemains;
        private BuildStatus _recoveryOutcome;
        private int _vanillaCharged;
        private System.Threading.Tasks.Task _editBackupWrite;
        internal bool SuiteCancelAfterRemoval, SuiteFailAfterApply;
        private readonly Dictionary<int, global::Game.Objects.Transform> _recoveryTransforms = new Dictionary<int, global::Game.Objects.Transform>();
        private sealed class RecoveryAppearance
        {
            public global::Game.Rendering.Bone[] Bones;
            public global::Game.Rendering.MeshColor[] Colors;
            public global::Game.Objects.Tree? Tree;
            public global::Game.Objects.Elevation? Elevation;
            public int? Seed;
        }
        private readonly Dictionary<int, RecoveryAppearance> _recoveryAppearance = new Dictionary<int, RecoveryAppearance>();
        private readonly Dictionary<long, MemberRole> _recoveryRoles = new Dictionary<long, MemberRole>();
        private sealed class RecoveryContractResolver : Newtonsoft.Json.Serialization.DefaultContractResolver
        {
            protected override IList<Newtonsoft.Json.Serialization.JsonProperty> CreateProperties(Type type, Newtonsoft.Json.MemberSerialization serialization)
            {
                var properties = base.CreateProperties(type, serialization);
                if (!type.IsValueType) return properties;
                var fields = new List<Newtonsoft.Json.Serialization.JsonProperty>();
                foreach (var property in properties)
                    if (type.GetField(property.UnderlyingName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public) != null) fields.Add(property);
                return fields;
            }
        }

        // Capture live geometry rather than composing the receipt: edits by other tools, exact curves,
        // prop height/tilt and the selected asset must survive a failed replacement.
        private void CaptureEditRecovery()
        {
            if (_recovering || _recoveryRequest != null) return;
            int target = _request.ReplaceGroup != 0 ? _request.ReplaceGroup : _request.BrushGroup;
            if (target == 0) return;
            if (_request.ReplaceGroup == 0 && _request.BrushRemove.Count == 0) return;
            var record = _registry.GetRecord(target);
            if (record == null) throw new InvalidOperationException("The edited group no longer exists");
            var saved = record.Clone();
            var plan = new ComposedPlan();
            var layer = new AreaPlan { GeneratorId = "edit-recovery" };
            plan.Layers.Add(layer);
            var members = new List<Entity>();
            _registry.GetMembers(target, members);
            var selected = _request.ReplaceGroup != 0 ? null : new HashSet<Entity>(_request.BrushRemove);
            _recoveryTransforms.Clear();
            _recoveryAppearance.Clear();
            _recoveryRoles.Clear();
            foreach (var entity in members)
            {
                if (selected != null && !selected.Contains(entity)) continue;
                if (!EntityManager.Exists(entity) || EntityManager.HasComponent<Deleted>(entity) || EntityManager.HasComponent<Temp>(entity))
                    throw new InvalidOperationException("An edited member is no longer stable");
                var tag = EntityManager.GetComponentData<FlowerPainterMember>(entity);
                if (tag.Kind == MemberKind.Auxiliary || tag.Kind == MemberKind.Node || tag.Role == MemberRole.Companion || tag.Role == MemberRole.ParkCompanion || tag.Role == MemberRole.OwnerArea) continue;
                if (!EntityManager.HasComponent<PrefabRef>(entity)) throw new InvalidOperationException("Cannot back up an edited prefab");
                var prefab = EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab;
                var name = _prefabs.GetPrefab<PrefabBase>(prefab)?.name;
                if (string.IsNullOrEmpty(name)) throw new InvalidOperationException("Cannot back up an edited prefab");
                var asset = new AssetRef(name);
                if (EntityManager.HasComponent<Edge>(entity) && EntityManager.HasComponent<Curve>(entity))
                {
                    var c = EntityManager.GetComponentData<Curve>(entity).m_Bezier;
                    var edge = EntityManager.GetComponentData<Edge>(entity);
                    var net = new PlannedNet { A = c.a, B = c.b, C = c.c, D = c.d, Asset = asset,
                        Role = tag.Role == MemberRole.ParkingAisle ? NetRole.DriveAisle : NetRole.Pathway,
                        FollowTerrain = false, PreserveCurve = true,
                        StartAttach = GroupRemovalSystem.Id(edge.m_Start), EndAttach = GroupRemovalSystem.Id(edge.m_End) };
                    if (EntityManager.HasComponent<Upgraded>(entity))
                    {
                        var f = EntityManager.GetComponentData<Upgraded>(entity).m_Flags;
                        net.Upgrades = new[] { "general:" + f.m_General, "left:" + f.m_Left, "right:" + f.m_Right };
                    }
                    _recoveryRoles[PlanWork.Key(new WorkItem { Kind = WorkKind.Net, Layer = 0, Index = layer.Nets.Count })] = tag.Role;
                    layer.Nets.Add(net);
                }
                else if (EntityManager.HasBuffer<global::Game.Areas.Node>(entity))
                {
                    var nodes = EntityManager.GetBuffer<global::Game.Areas.Node>(entity, true);
                    var ring = new float3[nodes.Length];
                    for (int i = 0; i < ring.Length; i++) ring[i] = nodes[i].m_Position;
                    if (tag.Role == MemberRole.Surface) layer.Surfaces.Add(new PlannedSurface { Ring = ring, Asset = asset });
                    else
                    {
                        _recoveryRoles[PlanWork.Key(new WorkItem { Kind = WorkKind.Area, Layer = 0, Index = layer.Areas.Count })] = tag.Role;
                        layer.Areas.Add(new PlannedArea { Ring = ring, Asset = asset, Role = AreaRole.Lot });
                    }
                }
                else if (EntityManager.HasComponent<global::Game.Objects.Transform>(entity))
                {
                    // Live functional buildings carry simulation state that a decorative definition cannot restore.
                    // Refuse before removal rather than pretending that replanning is a lossless backup.
                    if (EntityManager.HasComponent<global::Game.Buildings.Building>(entity))
                        throw new InvalidOperationException("This group contains a live building and cannot be replaced safely");
                    var tr = EntityManager.GetComponentData<global::Game.Objects.Transform>(entity);
                    var forward = math.mul(tr.m_Rotation, new float3(0, 0, 1));
                    _recoveryTransforms[layer.Objects.Count] = tr;
                    var appearance = new RecoveryAppearance();
                    if (EntityManager.HasBuffer<global::Game.Rendering.Bone>(entity))
                    { using var bones = EntityManager.GetBuffer<global::Game.Rendering.Bone>(entity, true).ToNativeArray(Allocator.Temp); appearance.Bones = bones.ToArray(); }
                    if (EntityManager.HasBuffer<global::Game.Rendering.MeshColor>(entity))
                    { using var colors = EntityManager.GetBuffer<global::Game.Rendering.MeshColor>(entity, true).ToNativeArray(Allocator.Temp); appearance.Colors = colors.ToArray(); }
                    if (EntityManager.HasComponent<global::Game.Objects.Tree>(entity)) appearance.Tree = EntityManager.GetComponentData<global::Game.Objects.Tree>(entity);
                    if (EntityManager.HasComponent<global::Game.Objects.Elevation>(entity)) appearance.Elevation = EntityManager.GetComponentData<global::Game.Objects.Elevation>(entity);
                    if (EntityManager.HasComponent<PseudoRandomSeed>(entity)) appearance.Seed = EntityManager.GetComponentData<PseudoRandomSeed>(entity).m_Seed;
                    _recoveryAppearance[layer.Objects.Count] = appearance;
                    layer.Objects.Add(new PlannedObject { Position = tr.m_Position, Yaw = math.atan2(forward.x, forward.z), Asset = asset,
                        Role = tag.Role == MemberRole.ParkingBay ? ObjectRole.ParkingBay : ObjectRole.Furniture });
                }
                else throw new InvalidOperationException("Cannot back up this edited member safely");
            }
            _recoveryRecord = saved;
            _recoveryRequest = new BuildRequest { Plan = plan, Recipe = saved.Recipe?.Clone(), Area = saved.Area,
                CustomName = saved.CustomName, BrushGroup = selected == null ? 0 : target,
                BrushRegions = saved.Footprint(), BrushErase = selected != null };
            var recoveryPath = System.IO.Path.Combine(Settings.ModPaths.DataFolder, "recovery-" + target + "-" + DateTime.UtcNow.Ticks + ".json");
            var snapshot = new { Record = saved, Plan = plan,
                Transforms = new Dictionary<int, global::Game.Objects.Transform>(_recoveryTransforms),
                Appearance = new Dictionary<int, RecoveryAppearance>(_recoveryAppearance) };
            _editBackupWrite = System.Threading.Tasks.Task.Run(() => Settings.ModPaths.WriteAllTextAtomic(recoveryPath,
                Newtonsoft.Json.JsonConvert.SerializeObject(snapshot,
                    new Newtonsoft.Json.JsonSerializerSettings { ContractResolver = new RecoveryContractResolver() })));
            Mod.Log.Info($"FlowerPainter edit backup: group {target}, {layer.Nets.Count} nets, {layer.Objects.Count} objects, {layer.Surfaces.Count + layer.Areas.Count} areas.");
        }

        private void AccountApplyCost()
        {
            long total = 0;
            using var temps = _tempQuery.ToComponentDataArray<Temp>(Allocator.Temp);
            foreach (var temp in temps)
                if ((temp.m_Flags & TempFlags.Cancel) == 0) total += temp.m_Cost;
            _vanillaCharged = (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, total));
        }

        private void ZeroRecoveryCosts()
        {
            using var temps = _tempQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in temps)
            {
                var temp = EntityManager.GetComponentData<Temp>(entity);
                temp.m_Cost = 0; EntityManager.SetComponentData(entity, temp);
            }
        }

        private bool TryRecoverEdit(BuildStatus outcome)
        {
            if (_recovering || !_replacementStarted || _recoveryRequest == null) return false;
            if (_applied && TryGetMoney(out var money, out var city))
            {
                long paid = (long)_vanillaCharged + _netCharged;
                money.Subtract((int)Math.Max(int.MinValue + 1L, Math.Min(int.MaxValue, -paid)));
                EntityManager.SetComponentData(city, money);
            }
            _recoveryOutcome = outcome;
            _recoveryPermanentRemains = _work != null && (_work.HasPermanentTerrain || HasPaint());
            var request = _recoveryRequest;
            _recovering = true;
            ResetTransaction();
            _request = request;
            _status = new BuildStatus { Phase = BuildPhase.Preparing, Progress = 0, ErrorPositions = new List<float3>(), Warnings = new List<PlanIssue>() };
            _startFrame = UnityEngine.Time.frameCount;
            Go(Step.Prepare);
            Mod.Log.Warn("FlowerPainter: restoring the edited content before returning the failed/cancelled build.");
            return true;
        }

        private void FinishEditRecovery()
        {
            if (!_recovering || _recoveryRecord == null) return;
            foreach (var e in _tagged)
            {
                if (!EntityManager.Exists(e) || !EntityManager.HasComponent<global::Game.Objects.Transform>(e) || !EntityManager.HasComponent<PrefabRef>(e)) continue;
                var p = EntityManager.GetComponentData<global::Game.Objects.Transform>(e).m_Position;
                if (_objectIndex != null && _objectIndex.TryMatch(p.xz, PrefabId(EntityManager.GetComponentData<PrefabRef>(e).m_Prefab), ObjectMatchTolerance, out var key, out _)
                    && _itemByKey.TryGetValue(key, out var item) && _recoveryTransforms.TryGetValue(item.Index, out var tr))
                {
                    EntityManager.SetComponentData(e, tr);
                    if (_recoveryAppearance.TryGetValue(item.Index, out var a))
                    {
                        if (a.Tree.HasValue) { if (!EntityManager.HasComponent<global::Game.Objects.Tree>(e)) EntityManager.AddComponent<global::Game.Objects.Tree>(e); EntityManager.SetComponentData(e, a.Tree.Value); }
                        if (a.Elevation.HasValue) { if (!EntityManager.HasComponent<global::Game.Objects.Elevation>(e)) EntityManager.AddComponent<global::Game.Objects.Elevation>(e); EntityManager.SetComponentData(e, a.Elevation.Value); }
                        if (a.Bones != null)
                        {
                            var bones = EntityManager.HasBuffer<global::Game.Rendering.Bone>(e) ? EntityManager.GetBuffer<global::Game.Rendering.Bone>(e) : EntityManager.AddBuffer<global::Game.Rendering.Bone>(e);
                            bones.Clear(); foreach (var bone in a.Bones) bones.Add(bone);
                        }
                        if (a.Colors != null)
                        {
                            var colors = EntityManager.HasBuffer<global::Game.Rendering.MeshColor>(e) ? EntityManager.GetBuffer<global::Game.Rendering.MeshColor>(e) : EntityManager.AddBuffer<global::Game.Rendering.MeshColor>(e);
                            colors.Clear(); foreach (var color in a.Colors) colors.Add(color);
                        }
                    }
                    if (!EntityManager.HasComponent<BatchesUpdated>(e)) EntityManager.AddComponent<BatchesUpdated>(e);
                }
            }
            if (!_registry.TryGetRecordEntity(_groupId, out var entity)) throw new InvalidOperationException("Restored group has no record");
            var record = _recoveryRecord.Clone();
            record.GroupId = _groupId;
            _registry.WriteRecord(entity, record);
            _registry.MarkDirty();
        }

        private void ClearEditRecovery()
        {
            if (_editBackupWrite != null)
                _editBackupWrite.ContinueWith(t => { var ignored = t.Exception; }, System.Threading.Tasks.TaskScheduler.Default);
            _editBackupWrite = null;
            _recoveryRequest = null; _recoveryRecord = null; _recoveryTransforms.Clear(); _recoveryAppearance.Clear();
            _recovering = false; _replacementStarted = false; _vanillaCharged = 0;
            _recoveryPermanentRemains = false; _recoveryRoles.Clear();
        }

        private bool SuiteStopAfterRemoval()
        {
            if (!SuiteCancelAfterRemoval || _recovering) return false;
            SuiteCancelAfterRemoval = false;
            BeginCleanup(BuildPhase.Cancelled, "FlowerPainter.Build.Cancelled");
            return true;
        }
    }
}
