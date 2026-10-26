using System.Collections.Generic;
using Newtonsoft.Json;
using FlowerPainter.Core;
using FlowerPainter.Core.Layers.Common;
using FlowerPainter.Core.Layers.Types.Floral;
using FlowerPainter.Core.Tool;
using FlowerPainter.Core.Geometry;
using Unity.Mathematics;

namespace FlowerPainter.Game.Tool
{
    public sealed partial class FlowerPainterToolSystem
    {
        private string _floralPath;
        private int _floralIndex = -1, _floralHandle;
        private FloralMotif _floralStart;
        private float2 _floralDown;
        private bool _floralDrawing;
        private System.Threading.Tasks.Task<List<string>> _floralFillTask;
        private int _floralFillVersion;
        public bool FloralPartitioning => _recipe?.Root?.Settings.S("shape", "flow") == "partition";
        private int _partitionDesign = -1, _partitionShape = -1;
        private string _partitionKey;
        private System.Threading.Tasks.Task<FloralPartitions> _partitionTask;
        private FloralPartitions PartitionMap()
        {
            if (_shape == null || !FloralPartitioning) return null;
            if (_partitionDesign != _designVersion || _partitionShape != ShapeVersion)
            {
                var keys = _recipe.Root.Settings.L("motifs");
                var key = string.Join("\n", keys);
                if (_partitionShape != ShapeVersion || key != _partitionKey)
                {
                    var region = LayerRegion.From(_shape.Ring);
                    var motifs = FloralMotif.Read(keys);
                    _partitionTask = System.Threading.Tasks.Task.Run(() => new FloralPartitions(region, motifs, System.Threading.CancellationToken.None));
                    _partitionKey = key;
                }
                _partitionDesign = _designVersion; _partitionShape = ShapeVersion;
            }
            if (_partitionTask?.IsFaulted == true)
            {
                Mod.Log.Error(_partitionTask.Exception, "FlowerPainter subdivision failed");
                _partitionTask = null;
            }
            return _partitionTask?.Status == System.Threading.Tasks.TaskStatus.RanToCompletion ? _partitionTask.Result : null;
        }
        public Region FloralHoveredPartition
        {
            get
            {
                if (!FloralEditing || _floralIndex != -2 || !_cursorValid) return null;
                var map = PartitionMap();
                int label = map?.At(_cursor.xz) ?? -1;
                return label > 0 ? map.Regions[label - 1] : null;
            }
        }
        public string FloralPath => _floralPath;
        public bool FloralDrawing => _floralDrawing;
        public int FloralIndex => _floralIndex;
        public bool FloralEditing => _stage == ToolStage.Design && !string.IsNullOrEmpty(_floralPath) && _floralIndex >= -2 && _floralIndex != -1;

        public void SelectFloralPattern(string path, int index)
        {
            CancelGesture(); ExitMarkerMode();
            _floralFillTask = null;
            _floralDrawing = index >= 1000;
            if (_floralDrawing) index -= 1000;
            if (index == -2) { _floralPath = path ?? "0"; _floralIndex = -2; PartitionMap(); Touch(); return; }
            if (index < 0) { _floralPath = null; _floralIndex = -1; Touch(); return; }
            var step = RecipePaths.Resolve(_recipe, path ?? "0");
            if (step?.Layer != "floral" || !FloralCarpet.HasMotifs(step.Settings.S("shape", "collage")) || _shape == null) return;
            if (index >= FloralMotif.Read(step.Settings.L("motifs")).Count) return;
            _floralPath = path ?? "0"; _floralIndex = index;
            if (_stage != ToolStage.Design) SetStage(ToolStage.Design);
            Touch();
        }
        private int _floralPatternsVersion = -1;
        private List<FloralMotif> _floralPatternsCache = new List<FloralMotif>();
        public List<FloralMotif> FloralPatterns()
        {
            if (_floralPatternsVersion == _designVersion) return _floralPatternsCache;
            _floralPatternsVersion = _designVersion;
            var step = _recipe?.Root;
            _floralPatternsCache = step?.Layer == "floral" && FloralCarpet.HasMotifs(step.Settings.S("shape", "collage"))
                ? FloralMotif.Read(step.Settings.L("motifs")) : new List<FloralMotif>();
            return _floralPatternsCache;
        }
        public float2 FloralOrigin
        {
            get
            {
                if (_shape == null) return float2.zero;
                FlowerPainter.Core.Geometry.Poly.Bounds(_shape.Ring2D(), out var min, out var max);
                return (min + max) * 0.5f;
            }
        }
        public float3 FloralGround(float2 p) => new float3(p.x, _site.SampleHeight(p), p.y);
        public float2 FloralHandlePoint(FloralMotif motif, int handle)
        {
            if (handle >= 10 && handle - 10 < motif.Points.Length) return motif.World(motif.Points[handle - 10] * motif.Size, FloralOrigin);
            if (handle == 0) return motif.Center(FloralOrigin);
            if (handle == 1 && motif.Kind == "curve") return motif.World(new float2(motif.Size * 0.5f + 8, motif.Width * 0.5f + 8), FloralOrigin);
            if (handle == 3) return motif.World(new float2(0, -motif.Width * 0.5f - 3), FloralOrigin);
            return motif.World(handle == 1 ? new float2(motif.Size * 0.5f, 0) : new float2(0, motif.Size * 0.5f + 5), FloralOrigin);
        }
        private bool UpdateFloralPatterns()
        {
            if (!FloralEditing) return false;
            var step = RecipePaths.Resolve(_recipe, _floralPath);
            if (step?.Layer != "floral" || !FloralCarpet.HasMotifs(step.Settings.S("shape", "collage")) || _shape == null)
            { SelectFloralPattern("", -1); return false; }
            var motifs = FloralMotif.Read(step.Settings.L("motifs"));
            if (_floralIndex == -2) {
                var partitions = FloralPartitioning ? PartitionMap() : null;
                if (_floralFillTask != null) {
                    if (!_floralFillTask.IsCompleted) return true;
                    var task = _floralFillTask; _floralFillTask = null;
                    if (task.IsFaulted) { Mod.Log.Error(task.Exception,"FlowerPainter region fill failed"); return true; }
                    if (_floralFillVersion == _designVersion) SetValue(_floralPath,"fills",JsonConvert.SerializeObject(task.Result));
                    return true;
                }
                _cursorValid = _in.HasHit; if (_in.HasHit) _cursor = _in.Hit;
                if (_in.RightPressed) { SelectFloralPattern("", -1); return true; }
                if (_in.LeftPressed && _in.HasHit && _in.OverWorld) {
                    if (FloralPartitioning && partitions == null) return true;
                    var region = LayerRegion.From(_shape.Ring); var origin = FloralOrigin;
                    var hit=_in.Hit.xz;
                    var fills = FloralFill.Read(step.Settings.L("fills"));
                    var style=step.Settings.S("activeFillStyle","single"); var flower=step.Settings.S("activeFillFlower","");
                    List<string> FillRegion() {
                        var map = partitions == null ? new FloralRegions(region, motifs, System.Threading.CancellationToken.None) : null;
                        int At(float2 p) => partitions != null ? partitions.At(p) : map.At(p);
                        int label = At(hit);
                        if (label > 0) {
                        fills.RemoveAll(f => At(origin + new float2(f.X,f.Z)) == label);
                        var local = hit - origin;
                        if(fills.Count<64) fills.Add(new FloralFill { X=local.x, Z=local.y, Style=style, Flower=flower });
                        }
                        var values = new List<string>(); foreach (var f in fills) values.Add(f.Encode());
                        return values;
                    }
                    // The exact subdivision is already cached; commit this click before leaving fill mode.
                    if (partitions != null) SetValue(_floralPath,"fills",JsonConvert.SerializeObject(FillRegion()));
                    else {
                        _floralFillVersion=_designVersion;
                        _floralFillTask=System.Threading.Tasks.Task.Run(FillRegion);
                    }
                }
                return true;
            }
            if (_floralIndex >= motifs.Count) { SelectFloralPattern("", -1); return false; }
            float2 LocalPoint(FloralMotif m, float2 p) {
                var d = p - m.Center(FloralOrigin); math.sincos(math.radians(m.Angle),out float sn,out float cs);
                return new float2(d.x*cs+d.y*sn,-d.x*sn+d.y*cs)/m.Size;
            }
            void SaveMotif(FloralMotif m) {
                motifs[_floralIndex]=m; var values=new List<string>(); foreach(var item in motifs) values.Add(item.Encode());
                SetValue(_floralPath,"motifs",JsonConvert.SerializeObject(values));
            }
            if (_floralDrawing) {
                if (_in.RightPressed) { _floralDrawing=false; Touch(); return true; }
                if (_in.LeftPressed && _in.HasHit && _in.OverWorld) {
                    var m=motifs[_floralIndex].Clone(); var points=new List<float2>(m.Points);
                    if (points.Count < 64) points.Add(LocalPoint(m,_in.Hit.xz)); m.Points=points.ToArray(); SaveMotif(m);
                }
                return true;
            }
            _cursorValid = _in.HasHit;
            if (_gesture == Gesture.Floral)
            {
                if (_in.RightPressed) { CancelGesture(); return true; }
                if (_in.LeftReleased)
                {
                    _gesture = Gesture.None; EndValuePreview(true); _floralStart = null; return true;
                }
                if (!_in.HasHit || !_in.OverWorld || _floralStart == null) return true;
                var moved = _floralStart.Clone();
                var center = moved.Center(FloralOrigin);
                if (_floralHandle >= 10) moved.Points[_floralHandle-10] = LocalPoint(moved,_in.Hit.xz);
                else if (_floralHandle == 3) moved.Width=math.clamp((math.distance(_in.Hit.xz,center)-3)*2,0.5f,100);
                else if (_floralHandle == 0)
                {
                    var d = _in.Hit.xz - _floralDown; moved.X += d.x; moved.Z += d.y;
                }
                else if (_floralHandle == 1)
                    moved.Size = math.clamp(_floralStart.Size * math.distance(_in.Hit.xz, center) / math.max(0.1f, math.distance(_floralDown, center)), 2, 2000);
                else
                {
                    var a = _floralDown - center; var b = _in.Hit.xz - center;
                    moved.Angle += math.degrees(math.atan2(b.y, b.x) - math.atan2(a.y, a.x));
                }
                motifs[_floralIndex] = moved;
                var values = new string[motifs.Count];
                for (int i = 0; i < values.Length; i++) values[i] = motifs[i].Encode();
                PreviewValue(_floralPath, "motifs", JsonConvert.SerializeObject(values));
                return true;
            }
            if (_in.RightPressed && _in.HasHit && _in.OverWorld) {
                var m=motifs[_floralIndex].Clone(); int point=-1; float nearestPoint=HandleRadius(_in.Hit)*2;
                for(int i=0;i<m.Points.Length;i++) {float d=math.distance(m.World(m.Points[i]*m.Size,FloralOrigin),_in.Hit.xz); if(d<nearestPoint){point=i;nearestPoint=d;}}
                if(point>=0 && m.Points.Length>2){var pts=new List<float2>(m.Points);pts.RemoveAt(point);m.Points=pts.ToArray();SaveMotif(m);}
                else SelectFloralPattern("", -1); return true;
            }
            if (!_in.HasHit || !_in.OverWorld) return true;
            _cursor = _in.Hit;
            if (!_in.LeftPressed) return true;
            var motif = motifs[_floralIndex];
            float nearest = math.clamp(HandleRadius(_in.Hit) * 1.5f, 0.8f, 4f);
            int picked = -1;
            for (int i = 0; i < 4 + motif.Points.Length; i++)
            {
                int h=i<4?i:i+6;
                if (FloralPartitioning && h == 3) continue;
                float d = math.distance(FloralHandlePoint(motif, h), _in.Hit.xz);
                if (d <= nearest) { nearest = d; picked = h; }
            }
            // A fragment can move its entire motif even when its centre is outside the frame.
            if (picked < 0 && motif.Covers(_in.Hit.xz, FloralOrigin)) picked = 0;
            if (picked >= 0)
            {
                _floralHandle = picked; _floralStart = motif.Clone(); _floralDown = _in.Hit.xz;
                _gesture = Gesture.Floral; Touch();
            }
            return true;
        }
        private bool FinishFloralDrawing() { if (!_floralDrawing) return false; _floralDrawing=false; Touch(); return true; }
    }
}
