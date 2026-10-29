using FlowerPainter.Core.Layers.Types.Floral;
using FlowerPainter.Core.Tool;
using Unity.Mathematics;

namespace FlowerPainter.Game.Tool
{
    public sealed partial class FlowerPainterToolSystem
    {
        private bool _placingFlag;
        private float2 _flagGrab, _flagStartCenter, _flagLastCenter;
        private bool _flagMoved;

        public bool DirectFlagEditing => _stage == ToolStage.Design && _shape != null && _recipe?.Root?.Layer == "floral"
            && ArtworkReady && _recipe.Root.Settings.B("flagDirect", false);
        private bool ArtworkReady => _recipe?.Root?.Settings.S("shape", "") == "flag"
            || (_recipe?.Root?.Settings.S("shape", "") == "image" && FlowerImage.IsReady(_recipe.Root.Settings));
        public bool PlacingArtwork => _placingFlag;
        public bool FlagDragging => _gesture == Gesture.FlagMove;

        private float2 DirectFlagCenter()
        {
            float2 center=float2.zero;
            foreach(var point in _draw.Points) center+=point.xz;
            return center/_draw.Points.Count;
        }

        private void UpdateFlagDrag()
        {
            _cursorValid=_in.HasHit;
            if (_in.HasHit) _cursor=_in.Hit;
            if (FlagDragging) {
                if (_in.RightPressed) { CancelGesture(); return; }
                if (_in.HasHit && _in.OverWorld) {
                    var center=_flagStartCenter+_in.Hit.xz-_flagGrab;
                    if (math.distancesq(center,_flagLastCenter)>.0001f) {
                        _flagLastCenter=center;
                        _flagMoved=true;
                        SetDirectFlagRing(center);
                        ShapeChanged(throttled:true);
                    }
                }
                if (_in.LeftReleased) {
                    _gesture=Gesture.None;
                    if (_flagMoved) { ShapeChanged(); Commit("FlowerPainter.Undo.MoveFlag"); }
                    Touch();
                }
                return;
            }
            if (!_in.LeftPressed || !_in.HasHit || !_in.OverWorld || _draw.Count!=4) return;
            var settings=_recipe.Root.Settings;
            var origin=DirectFlagCenter();
            var delta=_in.Hit.xz-origin;
            math.sincos(math.radians(settings.F("flagAngle",0)),out float sin,out float cos);
            var local=new float2(delta.x*cos+delta.y*sin,-delta.x*sin+delta.y*cos);
            var template=FlowerImage.For(settings);
            float width=math.clamp(settings.F("flagWidth",30),5,200);
            if (template.Color(local/new float2(width,width/template.Ratio)+.5f)<0) return;
            _flagGrab=_in.Hit.xz;
            _flagStartCenter=_flagLastCenter=origin;
            _flagMoved=false;
            _gesture=Gesture.FlagMove;
            Touch();
        }

        public void BeginFlagPlacement()
        {
            if (_stage != ToolStage.Design || _building || _recipe?.Root?.Layer != "floral" || !ArtworkReady) return;
            CancelGesture();
            _flowerHome=false;_flowerCompleted=false;
            _placingFlag = true;
            Touch();
        }

        private void UpdateFlagPlacement()
        {
            if (_stage != ToolStage.Design || !ArtworkReady) { _placingFlag=false; Touch(); return; }
            _cursorValid = _in.HasHit;
            if (_in.HasHit) _cursor = _in.Hit;
            if (_in.RightPressed) { _placingFlag=false; Touch(); return; }
            if (!_in.HasHit || !_in.OverWorld || !_in.LeftPressed) return;
            var center=_in.Hit.xz;
            _placingFlag=false;
            bool moving=_shape != null && _recipe.Root.Settings.B("flagDirect",false);
            if (!moving) {
                ClearSelectionState();
                _mode=SelectionMode.Draw;
                SetValue("0","flagDirect","true");
            }
            _mode=SelectionMode.Draw;
            SetDirectFlagRing(center);
            ShapeChanged();
            RebuildShapeIfDirty(true);
            if (_shape != null) { Commit("FlowerPainter.Undo.CloseArea"); SetStage(ToolStage.Design); }
        }

        private void SyncDirectFlagShape()
        {
            var step=_recipe?.Root;
            if (_shape == null || step?.Layer != "floral" || !ArtworkReady || !step.Settings.B("flagDirect",false)) return;
            var ring=_draw.Points;
            if (ring.Count != 4) return;
            float2 center=float2.zero;
            foreach(var point in ring) center+=point.xz;
            center*=.25f;
            var expected=DirectFlagCorners(center);
            bool same=true;
            for(int i=0;i<4;i++) if(math.distance(ring[i].xz,expected[i])>.01f) {same=false;break;}
            if (same) return;
            _mode=SelectionMode.Draw;
            SetDirectFlagRing(center);
            ShapeChanged();
            RebuildShapeIfDirty(true);
        }

        private float2[] DirectFlagCorners(float2 center)
        {
            var settings=_recipe.Root.Settings;
            float width=math.clamp(settings.F("flagWidth",30),5,200);
            float height=width/FlowerImage.For(settings).Ratio;
            math.sincos(math.radians(settings.F("flagAngle",0)),out float sin,out float cos);
            float2 World(float2 p)=>center+new float2(p.x*cos-p.y*sin,p.x*sin+p.y*cos);
            return new[]{World(new float2(-width,-height)*.5f),World(new float2(width,-height)*.5f),World(new float2(width,height)*.5f),World(new float2(-width,height)*.5f)};
        }

        private void SetDirectFlagRing(float2 center)
        {
            float3 P(float2 p)=>new float3(p.x,_site.SampleHeight(p),p.y);
            var ring=DirectFlagCorners(center);
            _draw.SetRing(new[]{P(ring[0]),P(ring[1]),P(ring[2]),P(ring[3])});
        }
    }
}
