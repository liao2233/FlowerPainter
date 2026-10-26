using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Colossal.IO.AssetDatabase;
using Colossal.Serialization.Entities;
using Game;
using Game.Prefabs;
using Game.SceneFlow;
using Game.UI.Menu;
using Newtonsoft.Json.Linq;
using FlowerPainter.Core.Layers.Types.Floral;
using Unity.Entities;
using UnityEngine;

namespace FlowerPainter.Game.Suite
{
    public sealed partial class SuiteRunner
    {
        private IEnumerator StepSaveReload(ScenarioState st,JObject rec)
        {
            string[] PatchNames() {
                var members=new List<Entity>();Services.Groups.GetMembers(st.Group,members);
                var prefabs=_world.GetExistingSystemManaged<PrefabSystem>();var em=_world.EntityManager;
                return members.Where(e=>em.HasComponent<PrefabRef>(e))
                    .Select(e=>prefabs.TryGetPrefab<PrefabBase>(em.GetComponentData<PrefabRef>(e).m_Prefab,out var p)?p.name:"")
                    .Where(n=>n.StartsWith(FlowerPatchMesh.Prefix,StringComparison.Ordinal)).OrderBy(n=>n).ToArray();
            }
            var before=PatchNames();rec["patchesBefore"]=before.Length;
            if(before.Length==0){rec["error"]="No flower patches in the built group";yield break;}
            yield return CloseTool();
            var menu=_world.GetExistingSystemManaged<MenuUISystem>();
            var preview=global::Game.UI.ScreenCaptureHelper.CreateRenderTarget("FlowerPainter suite save",680,383);
            global::Game.UI.ScreenCaptureHelper.CaptureScreenshot(Camera.main,preview,new MenuHelpers.SaveGamePreviewSettings());
            string name="FlowerPainter Patch Test "+DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
            var save=GameManager.instance.Save(name,menu.GetSaveInfo(false),AssetDatabase.user,preview);
            yield return Until(()=>save.IsCompleted,120,"save flower patches");
            if(!save.IsCompleted||save.IsFaulted||!save.Result){rec["error"]="Saving flower patches failed";yield break;}
            rec["saveName"]=name;
            var metadata=GameManager.instance.settings.userState.lastSaveGameMetadata;
            var load=GameManager.instance.Load(GameMode.Game,Purpose.LoadGame,metadata);
            yield return Until(()=>load.IsCompleted,180,"reload flower patches");
            if(!load.IsCompleted||load.IsFaulted||!load.Result){rec["error"]="Reloading flower patches failed";yield break;}
            yield return Frames(30);
            var after=PatchNames();rec["patchesAfter"]=after.Length;
            if(!before.SequenceEqual(after))rec["error"]="Flower patch prefabs changed or disappeared after reload";
        }
    }
}
