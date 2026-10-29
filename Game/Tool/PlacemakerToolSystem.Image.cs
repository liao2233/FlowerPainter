using System;
using System.Threading;
using System.Threading.Tasks;
using FlowerPainter.Core;
using FlowerPainter.Core.Tool;
using FlowerPainter.Game.Assets;

namespace FlowerPainter.Game.Tool
{
    public sealed partial class FlowerPainterToolSystem
    {
        private Task<FlowerImageImporter.Result> _imageImport;
        private Recipe _imageImportRecipe;
        private string _imageImportError;

        public void BeginImageImport() => BeginImageImport(FlowerImageImporter.Choose);
        public void PasteImage() => BeginImageImport(FlowerImageImporter.Paste);

        private void BeginImageImport(Func<FlowerImageImporter.Result> read)
        {
            if(_imageImport!=null || _building || _stage!=ToolStage.Design || _flowerHome || _flowerCompleted || _recipe?.Root?.Layer!="floral" || _recipe.Root.Settings.S("shape","")!="image")return;
            CancelGesture();
            _imageImportRecipe=_recipe;_imageImportError=null;
            var completion=new TaskCompletionSource<FlowerImageImporter.Result>();
            _imageImport=completion.Task;
            Mod.Log.Info("FlowerPainter: importing image via "+read.Method.Name);
            var thread=new Thread(()=> {
                try {completion.SetResult(read());}
                catch(Exception e) {completion.SetException(e);}
            }) {IsBackground=true,Name="FlowerPainter image import"};
            thread.SetApartmentState(ApartmentState.STA);thread.Start();Touch();
        }

        private void PollImageImport()
        {
            if(_imageImport==null || !_imageImport.IsCompleted)return;
            var task=_imageImport;_imageImport=null;
            if(task.IsFaulted) {
                _imageImportError=task.Exception.GetBaseException().Message;
                Mod.Log.Warn("FlowerPainter image import: "+_imageImportError);
            } else if(task.Result!=null) {
                if(_recipe!=_imageImportRecipe || _stage!=ToolStage.Design || _building)
                    _imageImportError="The design changed during import. Please import again.";
                else ApplyImportedImage(task.Result);
            }
            _imageImportRecipe=null;Touch();
        }

        internal void ApplyImportedImage(FlowerImageImporter.Result image)
        {
            _flowerHome=false;_flowerCompleted=false;
            bool replacePlaced=DirectFlagEditing && _draw.Count==4;
            CancelGesture();
            Edit(recipe=> {
                var values=recipe.Root.Settings.Values;
                values["imageData"]=image.Data;values["imageName"]=image.Name;
                values["imageWidth"]=image.Width;values["imageHeight"]=image.Height;
                values["shape"]="image";values["plantingSystem"]="patch";
                values["flagDirect"]=replacePlaced;
                return true;
            },"FlowerPainter.Undo.ImportImage");
            if(!replacePlaced) BeginFlagPlacement();
        }
    }
}
