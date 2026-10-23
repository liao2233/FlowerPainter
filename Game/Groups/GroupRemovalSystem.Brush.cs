using System;
using System.Collections.Generic;
using Game.Common;
using Game.Net;
using Game.Tools;
using FlowerPainter.Core.Build;
using Unity.Entities;

namespace FlowerPainter.Game.Groups
{
    public sealed partial class GroupRemovalSystem
    {
        public sealed class BrushRemoval
        {
            internal int Group, Frames;
            internal HashSet<Entity> Parts;
            public bool Complete, Failed;
        }
        private readonly Queue<BrushRemoval> _brushRemovals = new Queue<BrushRemoval>();

        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, global::Game.GameMode mode)
        {
            _brushRemovals.Clear();
            base.OnGamePreload(purpose, mode);
        }

        public BrushRemoval RemoveBrushParts(int group, IEnumerable<Entity> parts)
        {
            var request = new BrushRemoval { Group = group, Parts = new HashSet<Entity>(parts) };
            // Only orphan nodes of these edges may go. Nodes shared with surviving roads retain their tags and position.
            foreach (var e in new List<Entity>(request.Parts))
                if (EntityManager.Exists(e) && EntityManager.HasComponent<Edge>(e))
                {
                    var edge = EntityManager.GetComponentData<Edge>(e);
                    AddNode(edge.m_Start); AddNode(edge.m_End);
                }
            _brushRemovals.Enqueue(request);
            return request;
            void AddNode(Entity n)
            {
                if (!EntityManager.Exists(n) || !EntityManager.HasComponent<FlowerPainterMember>(n)) return;
                if (EntityManager.GetComponentData<FlowerPainterMember>(n).GroupId != group) return;
                if (EntityManager.HasBuffer<ConnectedEdge>(n))
                {
                    var edges = EntityManager.GetBuffer<ConnectedEdge>(n, true);
                    for (int i = 0; i < edges.Length; i++)
                        if (EntityManager.Exists(edges[i].m_Edge) && !EntityManager.HasComponent<Deleted>(edges[i].m_Edge)
                            && !request.Parts.Contains(edges[i].m_Edge)) return;
                }
                request.Parts.Add(n);
            }
        }

        private bool ProcessBrushRemoval()
        {
            if (_brushRemovals.Count == 0) return false;
            var r = _brushRemovals.Peek();
            try
            {
                r.Frames++;
                r.Parts.RemoveWhere(e => !EntityManager.Exists(e) || EntityManager.HasComponent<Deleted>(e));
                if (r.Parts.Count == 0) { r.Complete = true; _brushRemovals.Dequeue(); return true; }
                if (r.Frames > MaxPassesWithoutProgress) throw new InvalidOperationException("Brush removal timed out");
                var entities = new List<Entity>(r.Parts);
                if (ReferencedByTemporaries(entities)) return true;
                var byId = new Dictionary<long, Entity>();
                var members = new List<RemovalMember>();
                var connections = new Dictionary<long, List<long>>();
                foreach (var e in entities)
                {
                    if (!EntityManager.HasComponent<FlowerPainterMember>(e) || EntityManager.HasComponent<Temp>(e))
                        throw new InvalidOperationException("Brush target changed while editing");
                    var tag = EntityManager.GetComponentData<FlowerPainterMember>(e);
                    if (tag.GroupId != r.Group) throw new InvalidOperationException("Brush target changed group");
                    var m = new RemovalMember { Id = Id(e), Kind = Classify(e, tag) };
                    if (m.Kind == MemberKind.Edge)
                    {
                        var edge = EntityManager.GetComponentData<Edge>(e);
                        m.StartNode = Id(edge.m_Start); m.EndNode = Id(edge.m_End);
                    }
                    if (m.Kind == MemberKind.Node)
                    {
                        var list = new List<long>();
                        if (EntityManager.HasBuffer<ConnectedEdge>(e))
                        {
                            var edges = EntityManager.GetBuffer<ConnectedEdge>(e, true);
                            for (int i = 0; i < edges.Length; i++)
                                if (EntityManager.Exists(edges[i].m_Edge) && !EntityManager.HasComponent<Deleted>(edges[i].m_Edge)) list.Add(Id(edges[i].m_Edge));
                        }
                        connections[m.Id] = list;
                    }
                    members.Add(m); byId[m.Id] = e;
                }
                var pass = RemovalPlanner.NextPass(members, connections);
                var refresh = new HashSet<Entity>();
                foreach (var id in pass.Delete)
                {
                    var e = byId[id];
                    if (EntityManager.HasComponent<Edge>(e))
                    {
                        var edge = EntityManager.GetComponentData<Edge>(e);
                        refresh.Add(edge.m_Start); refresh.Add(edge.m_End);
                    }
                    PrepareDelete(e);
                    EntityManager.AddComponent<Deleted>(e);
                }
                // A node which acquired a foreign connection while queued must survive, still belonging to its group.
                foreach (var id in pass.Release) r.Parts.Remove(byId[id]);
                RefreshSurvivors(refresh);
                _registry.MarkDirty();
            }
            catch (Exception ex)
            {
                r.Failed = r.Complete = true;
                _brushRemovals.Dequeue();
                Mod.Log.Error(ex, "FlowerPainter: partial brush removal failed");
            }
            return true;
        }
    }
}
