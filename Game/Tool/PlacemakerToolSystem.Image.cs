using System;
using System.Diagnostics;
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

        public void BeginImageImport()
        {
            if(_imageImport!=null || _building || _stage!=ToolStage.Design || _recipe?.Root?.Layer!="floral")return;
            CancelGesture();
            _imageImportRecipe=_recipe;_imageImportError=null;
            var completion=new TaskCompletionSource<FlowerImageImporter.Result>();
            _imageImport=completion.Task;
            var owner=Process.GetCurrentProcess().MainWindowHandle;
            var thread=new Thread(()=> {
                try {completion.SetResult(FlowerImageImporter.Choose(owner));}
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
            Edit(recipe=> {
                var values=recipe.Root.Settings.Values;
                values["imageData"]=image.Data;values["imageName"]=image.Name;
                values["imageWidth"]=image.Width;values["imageHeight"]=image.Height;
                values["shape"]="image";values["plantingSystem"]="patch";
                values["flagDirect"]=true;
                return true;
            },"FlowerPainter.Undo.ImportImage");
        }
    }
}
