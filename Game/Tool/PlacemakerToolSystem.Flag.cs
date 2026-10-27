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
            if (_stage != ToolStage.Area || _building) return;
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
            ClearSelectionState();
            _mode=SelectionMode.Draw;
            SelectPreset("floral","flag-china");
            SetValue("0","flagDirect","true");
            SetDirectFlagRing(center);
            ShapeChanged();
            RebuildShapeIfDirty(true);
            if (_shape != null) { Commit("FlowerPainter.Undo.CloseArea"); SetStage(ToolStage.Design); }
        }

        private void SyncDirectFlagShape()
        {
            var step=_recipe?.Root;
            if (_shape == null || step?.Layer != "floral" || step.Settings.S("shape","") != "flag" || !step.Settings.B("flagDirect",false)) return;
            var ring=_shape.Ring;
            if (ring == null || ring.Length < 3) return;
            float2 lo=new float2(float.MaxValue),hi=new float2(float.MinValue);
            foreach(var point in ring) { lo=math.min(lo,point.xz);hi=math.max(hi,point.xz); }
            float width=math.clamp(step.Settings.F("flagWidth",30),5,200);
            float height=width/FlagCatalog.Get(step.Settings.S("flagTemplate","china")).Ratio;
            if (math.abs((hi.x-lo.x)-width)<.01f && math.abs((hi.y-lo.y)-height)<.01f) return;
            _mode=SelectionMode.Draw;
            SetDirectFlagRing((lo+hi)*.5f);
            ShapeChanged();
            RebuildShapeIfDirty(true);
        }

        private void SetDirectFlagRing(float2 center)
        {
            var settings=_recipe.Root.Settings;
            float width=math.clamp(settings.F("flagWidth",30),5,200);
            float height=width/FlagCatalog.Get(settings.S("flagTemplate","china")).Ratio;
            var lo=center-new float2(width,height)*.5f;
            var hi=center+new float2(width,height)*.5f;
            float3 P(float2 p)=>new float3(p.x,_site.SampleHeight(p),p.y);
            _draw.SetRing(new[]{P(lo),P(new float2(hi.x,lo.y)),P(hi),P(new float2(lo.x,hi.y))});
        }
    }
}
