using FlowerPainter.Core.Layers.Types.Floral;
using FlowerPainter.Core.Tool;
using Unity.Mathematics;

namespace FlowerPainter.Game.Tool
{
    public sealed partial class FlowerPainterToolSystem
    {
        private bool _placingFlag;

        public void BeginFlagPlacement()
        {
            if (_stage != ToolStage.Design || _building || _recipe?.Root?.Layer != "floral" || _recipe.Root.Settings.S("shape", "") != "flag") return;
            CancelGesture();
            _placingFlag = true;
            Touch();
        }

        private void UpdateFlagPlacement()
        {
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
            if (_shape == null || step?.Layer != "floral" || step.Settings.S("shape","") != "flag" || !step.Settings.B("flagDirect",false)) return;
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
            float height=width/FlagCatalog.Get(settings.S("flagTemplate","china")).Ratio;
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
