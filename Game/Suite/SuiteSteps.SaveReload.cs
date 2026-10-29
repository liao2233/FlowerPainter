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
        private IEnumerator StepArchiveNotice(ScenarioState st,JObject step,JObject rec)
        {
            yield return CloseTool();
            var archive=_world.GetExistingSystemManaged<Assets.FlowerPatchArchiveSystem>();
            archive.Warn("Suite notification check only; no model data was removed");
            yield return Frames(60);
            rec["notificationWhileClosed"]=!_tool.IsActive && global::Game.PSI.NotificationSystem.Exist(Assets.FlowerPatchArchiveSystem.NotificationId);
            if(!(bool)rec["notificationWhileClosed"]){rec["error"]="Archive notification missing while flower panel is closed";yield break;}
            yield return OpenTool();
            string previous=GUIUtility.systemCopyBuffer;
            try {
                yield return UiAct(st,rec,"copy diagnostics","copy-diagnostics","copyDiagnostics",null,
                    ()=>GUIUtility.systemCopyBuffer.StartsWith("FlowerPainter diagnostics"),()=>_tool.CopyDiagnostics(),mustChange:false);
                string copied=GUIUtility.systemCopyBuffer;
                rec["diagnosticsCopied"]=copied.Contains("Archive warning: FlowerPainter.Archive.Partial") && copied.Contains("Loaded Placemaker assemblies:") && copied.Contains("Last capture ms:");
                if(!(bool)rec["diagnosticsCopied"])rec["error"]="Diagnostic clipboard content is incomplete";
                rec["gameVersion"]=Application.version;
            } finally {GUIUtility.systemCopyBuffer=previous;}
        }
        private IEnumerator StepLoadSave(ScenarioState st,JObject step,JObject rec)
        {
            var wanted=(string)step["name"];
            var metadata=AssetDatabase.global.GetAssets<global::Game.Assets.SaveGameMetadata>()
                .FirstOrDefault(m=>string.Equals(m.target?.displayName??m.name,wanted,StringComparison.Ordinal));
            if(metadata==null){rec["error"]="Save not found: "+wanted;yield break;}
            var load=GameManager.instance.Load(GameMode.Game,Purpose.LoadGame,metadata);
            yield return Until(()=>load.IsCompleted,180,"load existing save");
            if(!load.IsCompleted||load.IsFaulted||!load.Result){rec["error"]="Loading existing save failed";yield break;}
            yield return Frames(30);
            st.Group=Services.Groups.AllGroups.FirstOrDefault();
            rec["loadedGroup"]=st.Group;
            if(st.Group==0)rec["error"]="Existing save contains no FlowerPainter group";
        }
        private IEnumerator StepSaveReload(ScenarioState st,JObject step,JObject rec)
        {
            string[] PatchNames() {
                var members=new List<Entity>();Services.Groups.GetMembers(st.Group,members);
                var prefabs=_world.GetExistingSystemManaged<PrefabSystem>();var em=_world.EntityManager;
                return members.Where(e=>em.HasComponent<PrefabRef>(e))
                    .Select(e=>prefabs.TryGetPrefab<PrefabBase>(em.GetComponentData<PrefabRef>(e).m_Prefab,out var p)?p.name:"")
                    .Where(n=>n.StartsWith(FlowerPatchMesh.Prefix,StringComparison.Ordinal)||n.StartsWith(FlowerPatchMesh.FlagPrefix,StringComparison.Ordinal)).OrderBy(n=>n).ToArray();
            }
            var before=PatchNames();rec["patchesBefore"]=before.Length;
            if(before.Length==0){rec["error"]="No flower patches in the built group";yield break;}
            yield return CloseTool();
            var menu=_world.GetExistingSystemManaged<MenuUISystem>();
            var preview=global::Game.UI.ScreenCaptureHelper.CreateRenderTarget("FlowerPainter suite save",680,383);
            global::Game.UI.ScreenCaptureHelper.CaptureScreenshot(Camera.main,preview,new MenuHelpers.SaveGamePreviewSettings());
            string name="FlowerPainter Patch Test "+DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
            var archive=_world.GetExistingSystemManaged<Assets.FlowerPatchArchiveSystem>();
            bool omitArchive=(bool?)step["omitArchive"]==true;
            archive.SuiteOmitNextArchive=omitArchive;
            var save=GameManager.instance.Save(name,menu.GetSaveInfo(false),AssetDatabase.user,preview);
            yield return Until(()=>save.IsCompleted,120,"save flower patches");
            if(!save.IsCompleted||save.IsFaulted||!save.Result){rec["error"]="Saving flower patches failed";yield break;}
            rec["saveName"]=name;
            rec["archiveBytes"]=archive.LastCaptureBytes;
            rec["archiveCaptureMs"]=archive.LastCaptureMilliseconds;
            rec["archiveBufferReleased"]=archive.PendingSaveBytes==0;
            if(archive.PendingSaveBytes!=0){rec["error"]="Serialized flower archive buffer was not released";yield break;}
            if ((bool?)step["evictNewPatches"] == true) {
                try { rec["evictedPatches"]=_world.GetExistingSystemManaged<Assets.FlowerPatchPrefabSystem>().EvictNewSuitePatches(before); }
                catch(Exception e) { rec["error"]="Suite cache isolation failed: "+e.Message; yield break; }
            }
            var metadata=GameManager.instance.settings.userState.lastSaveGameMetadata;
            var load=GameManager.instance.Load(GameMode.Game,Purpose.LoadGame,metadata);
            yield return Until(()=>load.IsCompleted,180,"reload flower patches");
            if(!load.IsCompleted||load.IsFaulted||!load.Result){rec["error"]="Reloading flower patches failed";yield break;}
            yield return Frames(30);
            var after=PatchNames();rec["patchesAfter"]=after.Length;
            rec["archiveWarning"]=archive.WarningKey;
            if(omitArchive && archive.WarningKey!="FlowerPainter.Archive.Missing")rec["error"]="Missing archive was not reported after loading flower references";
            if(!omitArchive && archive.HasWarning)rec["error"]="Unexpected archive warning after complete archive reload: "+archive.WarningKey;
            if(!before.SequenceEqual(after))rec["error"]="Flower patch prefabs changed or disappeared after reload";
        }
    }
}
