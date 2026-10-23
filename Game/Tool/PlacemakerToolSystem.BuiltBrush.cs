using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using FlowerPainter.Core;
using FlowerPainter.Core.Build;
using FlowerPainter.Core.Geometry;
using FlowerPainter.Core.Tool;
using FlowerPainter.Game.Groups;
using FlowerPainter.Game.Selection;
using Unity.Entities;
using Unity.Mathematics;

namespace FlowerPainter.Game.Tool
{
    public sealed partial class FlowerPainterToolSystem
    {
        private int _builtBrushGroup;
        private bool _builtBrushErase, _builtBrushLeft;
        private readonly BrushMask _builtBrushMask = new BrushMask(2f);
        public bool BrushingBuiltArea => _builtBrushGroup != 0;
        public BrushMask BuiltBrushMask => _builtBrushMask;
        public List<Region> BuiltBrushHoverRegions { get; private set; }
        private Task<ComposedPlan> _brushPlanning;
        private CancellationTokenSource _brushPlanningCancel;
        private BuildRequest _brushPlanningRequest;
        private float _brushPlanningStarted;

        private void CancelBrushPlanning()
        {
            var cancellation = _brushPlanningCancel;
            cancellation?.Cancel();
            if (_brushPlanning != null) _brushPlanning.ContinueWith(t => { var ignored = t.Exception; cancellation?.Dispose(); }, TaskScheduler.Default);
            else cancellation?.Dispose();
            _brushPlanningCancel = null; _brushPlanning = null; _brushPlanningRequest = null;
        }

        private void PumpBrushPlanning()
        {
            if (_brushPlanning == null) return;
            if (!_brushPlanning.IsCompleted)
            {
                if (UnityEngine.Time.realtimeSinceStartup - _brushPlanningStarted > 12f)
                { CancelBrushPlanning(); Flash(new LocText("FlowerPainter.Issue.PlanTimeout", 12)); Touch(); }
                return;
            }
            var task = _brushPlanning;
            var request = _brushPlanningRequest;
            _brushPlanning = null; _brushPlanningRequest = null;
            _brushPlanningCancel?.Dispose(); _brushPlanningCancel = null;
            if (task.IsCanceled) return;
            if (task.IsFaulted)
            {
                Mod.Log.Error(task.Exception, "FlowerPainter: brush planning failed");
                Flash(new LocText("FlowerPainter.Issue.PlanFailed", task.Exception.GetBaseException().Message)); Touch(); return;
            }
            request.Plan = task.Result;
            if (request.Plan.HasErrors)
            {
                foreach (var issue in request.Plan.AllIssues())
                    if (issue.Severity == IssueSeverity.Error) { Flash(new LocText(issue.Key, issue.Args), 8f); break; }
                Touch(); return;
            }
            StartBrushRequest(request);
        }

        private bool UpdateBuiltBrush()
        {
            BuiltBrushHoverRegions = null;
            if (_brushPlanning != null) { PumpBrushPlanning(); return true; }
            if (_builtBrushGroup == 0)
            {
                if (BrushRules.ForceSelection || _stage != ToolStage.Area || (_mode != SelectionMode.Paint && _mode != SelectionMode.BlockPaint)
                    || !BrushLive || !_in.HasHit || HasSelection()) return false;
                bool erase = _in.RightPressed || _in.Shift;
                int id = BuiltGroupAt(_in.Hit.xz, _brushSize * 0.5f, erase);
                if (id == 0) return false;
                BuiltBrushHoverRegions = (Services.Groups as GroupRegistrySystem)?.GetRecord(id)?.Footprint();
                _cursorValid = true; _cursor = _in.Hit;
                if (!(_in.LeftPressed || _in.RightPressed)) return true;
                _builtBrushGroup = id;
                _builtBrushErase = erase;
                _builtBrushLeft = !_in.RightPressed;
                _builtBrushMask.Clear();
                _gesture = erase ? Gesture.Erase : Gesture.Paint;
                _lastStroke = _in.Hit.xz;
                HideGhost();
                Flash(new LocText(erase ? "FlowerPainter.Hint.BrushEraseBuilt" : "FlowerPainter.Hint.BrushExtendBuilt"));
            }
            _cursorValid = _in.HasHit;
            if (_in.HasHit) _cursor = _in.Hit;
            bool held = _builtBrushLeft ? _in.LeftHeld : _in.RightHeld;
            if (held)
            {
                if (_in.HasHit)
                {
                    float radius = _brushSize * 0.5f;
                    if (_builtBrushMask.IsEmpty) _builtBrushMask.Stamp(_in.Hit.xz, radius, false, _brushSquare, _brushAngle);
                    else if (_builtBrushMask.Stroke(_lastStroke, _in.Hit.xz, radius, false, _brushSquare, _brushAngle)) _lastStroke = _in.Hit.xz;
                }
                return true;
            }
            int group = _builtBrushGroup;
            bool removing = _builtBrushErase;
            var stroke = _builtBrushMask.Outlines(true, true, 0f, 0f, 0);
            _builtBrushGroup = 0;
            BuiltBrushHoverRegions = null;
            _gesture = Gesture.None;
            _builtBrushMask.Clear();
            if (stroke.Count > 0) ApplyBuiltBrush(group, stroke, removing);
            return true;
        }

        private int BuiltGroupAt(float2 p, float radius, bool erase)
        {
            var registry = Services.Groups as GroupRegistrySystem;
            if (registry == null) return 0;
            int best = 0;
            float distance = float.MaxValue;
            foreach (int id in registry.AllGroups)
            {
                if (!registry.TryGetRecordEntity(id, out var entity) || EntityManager.HasComponent<FlowerPainterRemoval>(entity)
                    || EntityManager.HasComponent<FlowerPainterUnderConstruction>(entity)) continue;
                var record = registry.GetRecord(id);
                if (record?.Recipe == null) continue;
                var regions = record.Footprint();
                bool inside = BrushAreaEdit.Contains(regions, p);
                float d = inside ? -1f : float.MaxValue;
                foreach (var r in regions)
                {
                    d = math.min(d, Poly.DistanceToBoundary(r.Outer, p));
                    foreach (var h in r.Holes) d = math.min(d, Poly.DistanceToBoundary(h, p));
                    // A hole inside this project is a natural place to refill with the same recipe.
                    if (!erase && Poly.Contains(r.Outer, p)) d = math.min(d, 0f);
                }
                if (d > radius) continue;
                if (d < distance || d == distance && (best == 0 || id < best)) { best = id; distance = d; }
            }
            return best;
        }

        private void ApplyBuiltBrush(int group, List<Region> stroke, bool erase)
        {
            var registry = Services.Groups as GroupRegistrySystem;
            var record = registry?.GetRecord(group)?.Clone();
            if (record?.Recipe == null || Services.Build == null) return;
            var old = record.Footprint();
            var changed = erase ? Poly.Boolean(old, stroke, BoolOp.Intersection) : BrushAreaEdit.Added(old, stroke);
            if (changed.Count == 0) return;
            var request = new BuildRequest { BrushGroup = group, BrushErase = erase, Recipe = record.Recipe.Clone(), CustomName = record.CustomName };
            if (erase)
            {
                request.BrushRegions = BrushAreaEdit.Change(old, changed, true);
                // Removing the last footprint needs no replacement fragments. Retire the original
                // group after a successful empty transaction, including pieces extending over its rim.
                request.Plan = request.BrushRegions.Count == 0 ? new ComposedPlan() : CaptureBrushRemainder(group, changed, request.BrushRemove);
                request.Area = record.Area;
            }
            else
            {
                // Stops still protect roads, water and neighbouring projects; only this project's own footprint is exempt.
                var rules = BrushRules.ForOutline();
                var obstacles = new List<BrushObstacle>();
                var wetCuts = new List<Region>();
                foreach (var region in changed)
                {
                    Poly.Bounds(region.Outer, out var min, out var max);
                    Walls.Counted(min, max, group, rules, obstacles);
                    var wet = Walls.Water(min, max, 2f, rules);
                    if (wet != null)
                    {
                        var mask = new BrushMask(2f);
                        mask.PaintPolygon(region.Outer);
                        mask.EraseWhere(p => !wet(p));
                        wetCuts.AddRange(mask.Outlines(true, true, 0f, 0f, 0));
                    }
                }
                var rings = new List<IReadOnlyList<float2>>();
                foreach (var o in obstacles) rings.Add(o.Ring);
                var forbidden = Poly.UnionAll(rings);
                changed = Poly.Boolean(changed, forbidden, BoolOp.Difference);
                if (wetCuts.Count > 0) changed = Poly.Boolean(changed, wetCuts, BoolOp.Difference);
                if (changed.Count == 0) return;
                request.BrushRegions = BrushAreaEdit.Change(old, changed, false);
                var inputs = new List<(AreaShape shape, SiteContext site, List<Region> local)>();
                foreach (var region in request.BrushRegions)
                {
                    var local = Poly.Boolean(changed, new[] { region }, BoolOp.Intersection);
                    if (local.Count == 0) continue;
                    var shape = new AreaShape { Ring = Poly.To3D(region.Outer, p => _site.SampleHeight(p)), Source = AreaSource.Painted };
                    shape.Attachments = AttachmentFinder.Find(shape.Ring, _site.NetsAround(shape.Ring));
                    var site = _site.Capture(shape);
                    inputs.Add((shape, site, local));
                }
                request.Area = new AreaShape { Ring = Poly.To3D(changed[0].Outer, p => _site.SampleHeight(p)), Source = AreaSource.Painted };
                var cts = _brushPlanningCancel = new CancellationTokenSource();
                var token = cts.Token;
                _brushPlanningRequest = request;
                _brushPlanningStarted = UnityEngine.Time.realtimeSinceStartup;
                _brushPlanning = Task.Run(() =>
                {
                    var plan = new ComposedPlan();
                    foreach (var input in inputs)
                    {
                        token.ThrowIfCancellationRequested();
                        var generated = Composer.Compose(request.Recipe, input.shape, input.site, token);
                        var patch = BrushAreaEdit.ClipPlan(generated, input.local, input.site.HeightAt);
                        plan.Layers.AddRange(patch.Layers); plan.Footprints.AddRange(patch.Footprints);
                        plan.Issues.AddRange(patch.Issues);
                    }
                    return plan;
                }, token);
                Touch(); return;
            }
            StartBrushRequest(request);
        }

        private void StartBrushRequest(BuildRequest request)
        {
            Services.Assets?.ResolveAutomatic(request.Plan);
            _feedback.Started();
            if (!Services.Build.Start(request)) { Flash(new LocText("FlowerPainter.Problem.CannotEdit")); return; }
            _building = true;
            _buildStatus = default;
            _permanentAck = null;
            SetStage(ToolStage.Building);
            Mod.Log.Info($"FlowerPainter brush queued: group {request.BrushGroup}, erase={request.BrushErase}, remove={request.BrushRemove.Count}, regions={request.BrushRegions.Count}");
        }

        /// <summary>Only touched members are replaced; all survivors retain their prefab, curve and location.</summary>
        private ComposedPlan CaptureBrushRemainder(int group, List<Region> cut, List<Entity> remove)
        {
            var plan = new ComposedPlan();
            var layer = new AreaPlan { GeneratorId = "brush-edit" };
            plan.Layers.Add(layer);
            var members = new List<Entity>();
            Services.Groups.GetMembers(group, members);
            var prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();
            foreach (var e in members)
            {
                if (!EntityManager.Exists(e) || !EntityManager.HasComponent<PrefabRef>(e)) continue;
                var tag = EntityManager.GetComponentData<FlowerPainterMember>(e);
                if (tag.Role == MemberRole.Carrier || tag.Role == MemberRole.Companion || tag.Role == MemberRole.ParkCompanion) continue;
                var prefab = EntityManager.GetComponentData<PrefabRef>(e).m_Prefab;
                var name = prefabs.GetPrefab<PrefabBase>(prefab)?.name;
                if (string.IsNullOrEmpty(name)) throw new InvalidOperationException("Cannot resolve a brush target's prefab");
                var asset = new AssetRef { Id = name };
                if (EntityManager.HasComponent<Edge>(e) && EntityManager.HasComponent<Curve>(e))
                {
                    var c = EntityManager.GetComponentData<Curve>(e).m_Bezier;
                    var edge = EntityManager.GetComponentData<Edge>(e);
                    var n = new PlannedNet { A = c.a, B = c.b, C = c.c, D = c.d, Asset = asset,
                        Role = tag.Role == MemberRole.ParkingAisle ? NetRole.DriveAisle : EntityManager.HasComponent<RoadData>(prefab) ? NetRole.Street : NetRole.Pathway,
                        FollowTerrain = false, PreserveCurve = true,
                        StartAttach = GroupRemovalSystem.Id(edge.m_Start), EndAttach = GroupRemovalSystem.Id(edge.m_End) };
                    if (EntityManager.HasComponent<Upgraded>(e))
                    {
                        var f = EntityManager.GetComponentData<Upgraded>(e).m_Flags;
                        n.Upgrades = new[] { "general:" + f.m_General, "left:" + f.m_Left, "right:" + f.m_Right };
                    }
                    var kept = BrushAreaEdit.Clip(n, cut, false);
                    if (kept.Count == 1 && math.distancesq(kept[0].A, n.A) < 1e-6f && math.distancesq(kept[0].D, n.D) < 1e-6f) continue;
                    remove.Add(e);
                    layer.Nets.AddRange(kept);
                }
                else if (EntityManager.HasBuffer<global::Game.Areas.Node>(e))
                {
                    var nodes = EntityManager.GetBuffer<global::Game.Areas.Node>(e, true);
                    var ring = new float3[nodes.Length];
                    for (int i = 0; i < nodes.Length; i++) ring[i] = nodes[i].m_Position;
                    if (ring.Length < 3) continue;
                    var overlap = Poly.Boolean(new[] { new Region(Poly.To2D(ring)) }, cut, BoolOp.Intersection);
                    float overlapArea = 0f; foreach (var region in overlap) overlapArea += region.Area;
                    if (overlapArea < 0.01f) continue;
                    var kept = BrushAreaEdit.Clip(ring, cut, false, p => _site.SampleHeight(p));
                    remove.Add(e);
                    foreach (var r in kept)
                    {
                        if (tag.Role == MemberRole.Surface) layer.Surfaces.Add(new PlannedSurface { Ring = r, Asset = asset, Role = SurfaceRole.Decorative });
                        else layer.Areas.Add(new PlannedArea { Ring = r, Asset = asset, Role = AreaRole.Lot });
                    }
                }
                else if (EntityManager.HasComponent<global::Game.Objects.Transform>(e))
                {
                    var tr = EntityManager.GetComponentData<global::Game.Objects.Transform>(e);
                    if (BrushAreaEdit.Contains(cut, tr.m_Position.xz)) remove.Add(e);
                }
            }
            // Refresh zoning only within the erased footprint. Terrain heights remain as built.
            return plan;
        }
    }
}
