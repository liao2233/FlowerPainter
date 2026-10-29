using System.Collections.Generic;
using FlowerPainter.Core;
using FlowerPainter.Core.Tool;
using FlowerPainter.Core.Layers.Types.Floral;

namespace FlowerPainter.Game.Tool {
    public sealed partial class FlowerPainterToolSystem {
        private bool _flowerHome=true, _flowerCompleted;
        private readonly Dictionary<string,ToolSnapshot> _flowerDrafts=new Dictionary<string,ToolSnapshot>();
        private ToolSnapshot _beforeFlowerEdit;
        public bool FlowerWorkflowIdle => FlowerWorkflow && (_flowerHome || _flowerCompleted);
        private bool FlowerWorkflow => _recipe?.Root?.Layer=="floral";
        internal bool FlowerArtwork => FlowerWorkflow && (_recipe.Root.Settings.S("shape","")=="flag" || _recipe.Root.Settings.S("shape","")=="image");
        private string FlowerMode => ModeKey(_recipe?.Root?.Settings.S("shape","flow"));
        private static string ModeKey(string shape)=>shape=="flag"||shape=="image"||shape=="partition"||shape=="collage"?shape:"flow";
        public void ChoosePlanting(string mode) {
            if(_building || !FlowerWorkflow)return;
            if(mode!="" && mode!="flow" && mode!="partition" && mode!="collage" && mode!="flag" && mode!="image")return;
            if(mode!="" && mode==FlowerMode && !_flowerHome && !_flowerCompleted)return;
            _panelCollapsed=false;
            CancelGesture();ExitMarkerMode();SelectFloralPattern("",-1);
            if(_editingGroup!=0) CancelFlowerEdit();
            if(!_flowerCompleted)_flowerDrafts[FlowerMode]=Capture();
            _flowerCompleted=false;
            if(mode=="") {_flowerHome=true;HideGhost();SetStage(ToolStage.Design);Touch();return;}
            if(mode!=FlowerMode) {
                if(_flowerDrafts.TryGetValue(mode,out var draft)) Restore(draft);
                else {
                    if(FlowerArtwork || mode=="flag" || mode=="image")ClearSelectionState();_mode=SelectionMode.Draw;
                    _recipe=_recipe.Clone();_recipe.Root.Settings=new FloralLayer().Defaults();
                    _recipe.Root.Settings.Values["shape"]=mode;
                    _recipe.Root.Markers.Clear();_recipe.Root.Children.Clear();
                    _recipeId="";DesignChanged();
                }
            }
            _flowerHome=false;_managedGroup=0;_editingGroup=0;
            _history.Reset(Capture());
            SetStage(FlowerArtwork || _shape!=null?ToolStage.Design:ToolStage.Area);Touch();
        }
        public void RepeatFlowers() {
            if(_building || !FlowerWorkflow)return;
            CancelGesture();ClearSelectionState();_editingGroup=0;_managedGroup=0;
            _beforeFlowerEdit=null;_flowerCompleted=false;_flowerHome=false;
            DesignChanged();_history.Reset(Capture());
            SetStage(FlowerArtwork?ToolStage.Design:ToolStage.Area);
            if(FlowerArtwork)BeginFlagPlacement();
            Touch();
        }
        public void CancelFlowerEdit() {
            if(_building || _editingGroup==0)return;
            CancelGesture();_editingGroup=0;
            if(_beforeFlowerEdit!=null)Restore(_beforeFlowerEdit);else ClearSelectionState();
            _beforeFlowerEdit=null;_flowerHome=true;_flowerCompleted=false;
            _history.Reset(Capture());SetStage(ToolStage.Design);Touch();
        }
        private void PickFlowerGroup() {
            UpdateManage();
            if(_managedGroup!=0)EditGroup(_managedGroup);
        }
    }
}
