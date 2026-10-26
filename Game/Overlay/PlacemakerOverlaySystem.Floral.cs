using Colossal.Mathematics;
using Game.Rendering;
using FlowerPainter.Core.Tool;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;
using FlowerPainter.Core.Layers.Common;

namespace FlowerPainter.Game.Overlay
{
    public sealed partial class FlowerPainterOverlaySystem
    {
        private int _floralShapeVersion = -1;
        private LayerRegion _floralRegion;
        private int _floralDesignVersion = -1;
        private readonly List<string> _floralKeys = new List<string>();
        private readonly List<float2[]> _floralOutlines = new List<float2[]>();
        private readonly List<bool[]> _floralInside = new List<bool[]>();
        private void DrawFloralPatterns(OverlayRenderSystem.Buffer buffer, float w)
        {
            if (_tool.Stage != ToolStage.Design || _tool.Shape == null) return;
            var selectedRegion = _tool.FloralSelectedPartition;
            if (selectedRegion != null) {
                var color = new Color(1f, 0.78f, 0.22f, 1f);
                Ring(buffer, FlowerPainter.Core.Geometry.Poly.To3D(selectedRegion.Outer, p => _tool.FloralGround(p).y), color, w * 2f);
                foreach (var hole in selectedRegion.Holes)
                    Ring(buffer, FlowerPainter.Core.Geometry.Poly.To3D(hole, p => _tool.FloralGround(p).y), color, w * 2f);
            }
            var hovered = _tool.FloralHoveredPartition;
            if (hovered != null)
            {
                var highlight = new Color(0.2f, 1f, 0.8f, 0.95f);
                Ring(buffer, FlowerPainter.Core.Geometry.Poly.To3D(hovered.Outer, p => _tool.FloralGround(p).y), highlight, w * 1.8f);
                foreach (var hole in hovered.Holes)
                    Ring(buffer, FlowerPainter.Core.Geometry.Poly.To3D(hole, p => _tool.FloralGround(p).y), highlight, w * 1.8f);
            }
            var motifs = _tool.FloralPatterns();
            if (motifs.Count == 0) return;
            if (_floralShapeVersion != _tool.ShapeVersion)
            {
                _floralShapeVersion = _tool.ShapeVersion;
                _floralRegion = LayerRegion.From(_tool.Shape.Ring);
                _floralKeys.Clear(); _floralOutlines.Clear(); _floralInside.Clear();
            }
            var region = _floralRegion;
            if (region == null) return;
            var origin = (region.Min + region.Max) * 0.5f;
            bool changed = _floralDesignVersion != _tool.DesignVersion;
            _floralDesignVersion = _tool.DesignVersion;
            for (int i = 0; i < motifs.Count; i++)
            {
                var motif = motifs[i];
                bool selected = _tool.FloralEditing && i == _tool.FloralIndex;
                while (_floralKeys.Count <= i) { _floralKeys.Add(null); _floralOutlines.Add(null); _floralInside.Add(null); }
                var key = changed || _floralKeys[i] == null ? motif.Encode() : _floralKeys[i];
                if (_floralKeys[i] != key)
                {
                    _floralKeys[i] = key;
                    var outline = motif.Outline(origin, 96);
                    var inside = new bool[outline.Length - 1];
                    for (int k = 0; k < inside.Length; k++) inside[k] = region.Contains((outline[k] + outline[k + 1]) * 0.5f);
                    _floralOutlines[i] = outline; _floralInside[i] = inside;
                }
                var points = _floralOutlines[i];
                var previous = _tool.FloralGround(points[0]);
                for (int k = 0; k + 1 < points.Length; k++)
                {
                    bool inside = _floralInside[i][k];
                    var color = selected ? new Color(1f, 0.78f, 0.22f, inside ? 0.9f : 0.35f) : new Color(0.8f, 0.9f, 1f, inside ? 0.4f : 0.15f);
                    var next = _tool.FloralGround(points[k + 1]);
                    buffer.DrawLine(color, new Line3.Segment(previous, next), w * (selected ? 1f : 0.55f));
                    previous = next;
                }
                if (!selected) continue;
                var c = _tool.FloralGround(_tool.FloralHandlePoint(motif, 0));
                for (int iHandle = 0; iHandle < 4 + motif.Points.Length; iHandle++)
                {
                    int h = iHandle < 4 ? iHandle : iHandle + 6;
                    if (_tool.FloralPartitioning && h == 3) continue;
                    var p = _tool.FloralGround(_tool.FloralHandlePoint(motif, h));
                    var color = h == 0 ? Color.white : h == 1 ? new Color(0.35f, 1, 0.55f) : new Color(1, 0.65f, 0.2f);
                    if (h != 0) buffer.DrawDashedLine(new Color(color.r, color.g, color.b, 0.4f), new Line3.Segment(c, p), w * 0.6f, w * 3f, w * 3f);
                    buffer.DrawCircle(color, new Color(color.r, color.g, color.b, 0.35f), w * 0.5f, 0, new float2(0, 1), p, w * 6f);
                }
            }
        }
    }
}
