// What the panel and the cursor tooltips show: phase-aware hints and the contract's ToolState.
// One right-click rule everywhere: right-click removes the thing you would add with a click
// (corner, node, block, paint, marker) — never anything else.

using System.Collections.Generic;
using FlowerPainter.Core.Build;
using FlowerPainter.Core.Geometry;
using FlowerPainter.Core.Tool;

namespace FlowerPainter.Game.Tool
{
    public sealed partial class FlowerPainterToolSystem
    {
        /// <summary>(Area overhaul) The legend rows for what the overlay shows while choosing the area.</summary>
        private List<PreviewLegend.Entry> AreaLegendNow()
        {
            var i = new AreaLegend.Input { Mode = _mode, HasShape = _shape != null, AreaM2 = _shape != null ? AreaM2 : 0 };
            switch (_mode)
            {
                case SelectionMode.Block:
                    i.HasFill = _hoverFace != null;
                    i.Doors = _hoverFace?.Doors.Count ?? 0;
                    i.Reach = _hoverFace?.Reach != null;
                    i.Stops = _hoverFace?.Stops.Count ?? 0;
                    break;
                case SelectionMode.BlockPaint:
                    var cur = BlockPaintCurrent;
                    i.HasFill = cur?.Ring != null || _hoverFace != null;
                    i.Doors = cur?.Doors.Count ?? _hoverFace?.Doors.Count ?? 0;
                    i.Stops = cur?.Obstacles.Count ?? _hoverFace?.Stops.Count ?? 0;
                    break;
                default:
                    i.LeftOut = _leftOut?.Count ?? 0;
                    i.Nodes = CornersOnNodes;
                    break;
            }
            return AreaLegend.Compute(i);
        }

        /// <summary>Hints for the current situation, most important first (tooltips show the first ones).</summary>
        public List<HintInfo> Hints()
        {
            var h = new List<HintInfo>(8);
            if (FloralEditing) { h.Add(new HintInfo(_floralIndex == -2 ? FloralPartitioning ? "FlowerPainter.UI.Partitions.SelectTip" : "FlowerPainter.UI.Flowers.RegionTip" : FloralPartitioning ? "FlowerPainter.Floral.UI.PartitionDragHint" : "FlowerPainter.Floral.UI.DragHint")); h.Add(new HintInfo("FlowerPainter.Hint.Back", "Esc")); return h; }
            var flash = FlashText;
            if (flash != null) h.Add(new HintInfo(flash.Key) { Text = flash });

            if (_stage == ToolStage.Building)
            {
                h.Add(new HintInfo("FlowerPainter.Hint.Building"));
                return h;
            }
            if (_markerKind != null)
            {
                h.Add(new HintInfo(MarkerKinds.IsInterior(_markerKind) ? "FlowerPainter.Floral.Hint.PlacePoint" : "FlowerPainter.Hint.MarkerPlace"));
                h.Add(new HintInfo("FlowerPainter.Hint.MarkerRemove"));
                h.Add(new HintInfo("FlowerPainter.Hint.MarkerDone", "Esc"));
                return h;
            }
            if (_stage == ToolStage.Manage)
            {
                h.Add(new HintInfo("FlowerPainter.Hint.ManagePick"));
                if (_managedGroup != 0) h.Add(new HintInfo("FlowerPainter.Hint.ManageDeselect"));
                h.Add(new HintInfo("FlowerPainter.Hint.Back", "Esc"));
                return h;
            }

            switch (_mode)
            {
                case SelectionMode.Draw:
                    if (_draw.Rectangle)
                    {
                        h.Add(new HintInfo(_draw.Closed ? "FlowerPainter.Hint.RectangleResize" : "FlowerPainter.Hint.RectangleDraw"));
                        if (!_draw.Closed && _draw.Count > 0) h.Add(new HintInfo("FlowerPainter.Hint.DrawRemoveLast"));
                        break;
                    }
                    if (!_draw.Closed)
                    {
                        h.Add(new HintInfo(_draw.Count >= PolygonEditor.MinPoints ? "FlowerPainter.Hint.DrawAddOrClose" : "FlowerPainter.Hint.DrawAdd"));
                        if (_draw.Count > 0) h.Add(new HintInfo("FlowerPainter.Hint.DrawRemoveLast"));
                        if (_lastSnap.Kind == SnapKind.RoadNode) h.Add(new HintInfo("FlowerPainter.Hint.DrawNodes"));
                        h.Add(new HintInfo("FlowerPainter.Hint.DrawNoSnap", "Alt"));
                    }
                    else
                    {
                        if (_gesture == Gesture.DragEdge) h.Add(new HintInfo("FlowerPainter.Hint.DrawEdgeDragging"));
                        else if (_hoverPoint >= 0) { h.Add(new HintInfo("FlowerPainter.Hint.DrawMovePoint")); h.Add(new HintInfo("FlowerPainter.Hint.DrawRemovePoint")); }
                        else if (_hoverEdge >= 0)
                        {
                            h.Add(new HintInfo("FlowerPainter.Hint.DrawInsertDrag"));
                            h.Add(new HintInfo("FlowerPainter.Hint.DrawInsertClick", "Ctrl"));
                            h.Add(new HintInfo("FlowerPainter.Hint.DrawMoveEdge", "Shift"));
                        }
                        else h.Add(new HintInfo("FlowerPainter.Hint.DrawEdit"));
                    }
                    break;
                case SelectionMode.Block:
                    h.Add(new HintInfo(_blocks.Seeds.Count == 0 ? "FlowerPainter.Hint.BlockPick" : "FlowerPainter.Hint.BlockReplace"));
                    h.Add(new HintInfo("FlowerPainter.Hint.BlockAdd", "Shift"));
                    if (_blocks.Seeds.Count > 0) h.Add(new HintInfo("FlowerPainter.Hint.BlockRemove"));
                    break;
                case SelectionMode.Perimeter:
                    if (_perimeter.Closed) h.Add(new HintInfo("FlowerPainter.Hint.PerimeterReopen"));
                    else
                    {
                        h.Add(new HintInfo(_perimeter.Nodes.Count >= 2 ? "FlowerPainter.Hint.PerimeterAddOrClose" : "FlowerPainter.Hint.PerimeterAdd"));
                        if (_perimeter.Nodes.Count >= 3 && _shape != null) h.Add(new HintInfo("FlowerPainter.Hint.PerimeterAutoClose"));
                        if (_perimeter.Nodes.Count > 0) h.Add(new HintInfo("FlowerPainter.Hint.PerimeterRemoveLast"));
                    }
                    break;
                case SelectionMode.Paint:
                    if (_stage != ToolStage.Area) break;   // s15: the brush only works in the Area stage
                    h.Add(new HintInfo("FlowerPainter.Hint.PaintPaint"));
                    h.Add(new HintInfo("FlowerPainter.Hint.PaintErase"));
                    if (_brushSquare) h.Add(new HintInfo("FlowerPainter.Hint.PaintRotate", "Shift"));
                    break;
                case SelectionMode.BlockPaint:
                    if (_stage != ToolStage.Area) break;
                    var rules = BrushRules;
                    h.Add(new HintInfo(!rules.Stops(BrushObstacleKind.Road) ? "FlowerPainter.Hint.PaintPaint" : !BlockPaintHasTarget ? "FlowerPainter.Hint.BlockPaintStart" : "FlowerPainter.Hint.BlockPaintPaint"));
                    h.Add(new HintInfo("FlowerPainter.Hint.BlockPaintErase"));
                    if (_brushSquare) h.Add(new HintInfo("FlowerPainter.Hint.PaintRotate", "Shift"));
                    if (BlockPaintCurrent?.Obstacles.Count > 0) h.Add(new HintInfo("FlowerPainter.Hint.BlockPaintBlocked"));
                    if (BlockPaintHasTarget && rules.Stops(BrushObstacleKind.Road)) h.Add(new HintInfo("FlowerPainter.Hint.BlockPaintNewBlock"));
                    break;
            }
            AddBuildingHint(h);
            if (_history.CanUndo) h.Add(new HintInfo("FlowerPainter.Hint.Undo", "Ctrl", "Z"));
            var p = StageMachine.Primary(_stage, Facts());
            if (p.Enabled && p.Kind != PrimaryKind.None) h.Add(new HintInfo("FlowerPainter.Hint.Primary", "Enter"));
            h.Add(new HintInfo(HasSelection() || _stage != ToolStage.Area ? "FlowerPainter.Hint.Back" : "FlowerPainter.Hint.Close", "Esc"));
            return h;
        }

        private LocText MarkerLabel(string path, string kind)
        {
            var step = RecipePaths.Resolve(_recipe, path);
            var gen = step != null ? FlowerPainter.Core.GeneratorRegistry.Get(step.Layer) : null;
            foreach (var k in MarkerKinds.For(gen))
                if (k.Kind == kind) return new LocText(k.LabelKey);
            return new LocText("FlowerPainter.Marker." + kind);
        }

        public ToolStateInput StateInput()
        {
            var facts = Facts();
            var s = new ToolStateInput
            {
                Open = IsActive || _building,
                Stage = _stage,
                Reachable = StageMachine.Reachable(facts),
                Mode = _mode,
                BrushSquare = _brushSquare,
                BrushSize = _brushSize,
                BrushAngle = _brushAngle,
                BrushRules = BrushRules,
                AreaValid = _shape != null && facts.AreaProblem == null,
                AreaClosed = AreaClosed,
                PointCount = PointCount,
                AreaM2 = AreaM2,
                PerimeterM = PerimeterM,
                AreaProblem = facts.AreaProblem,
                FloralPath = FloralEditing ? _floralPath : null,
                FloralDrawing = _floralDrawing,
                FloralIndex = _floralIndex,
                FloralPartitions = FloralPartitionState(),
                Primary = StageMachine.Primary(_stage, facts),
                CanUndo = CanUndo,
                CanRedo = CanRedo,
                UndoLabel = UndoLabel,
                RedoLabel = RedoLabel,
                MarkerStepPath = _markerPath,
                MarkerKind = _markerKind,
                MarkerLabel = _markerKind != null ? MarkerLabel(_markerPath, _markerKind) : null,
                Hints = Hints(),
                Planning = facts.Planning,
                PlanningProgress = PlanningProgress,
                BuildProgress = _building ? BuildProgress : (float?)null,
                EditingGroup = _editingGroup,
                AnarchyAvailable = AnarchyAvailable,
                AnarchyEnabled = _anarchy,
                PanelCollapsed = _panelCollapsed,
                PreviewMode = PreviewModeSelected,
                GhostPending = GhostPending,
                LegendVisibility = LegendVisibility,
                AreaLegend = _stage == ToolStage.Area ? AreaLegendNow() : null,
            };
            return s;
        }
    }
}
