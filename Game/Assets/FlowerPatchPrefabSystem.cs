using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Colossal.IO.AssetDatabase;
using Colossal.Mathematics;
using Game;
using Game.Prefabs;
using FlowerPainter.Core;
using FlowerPainter.Core.Layers.Types.Floral;
using Newtonsoft.Json;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;

namespace FlowerPainter.Game.Assets
{
    // Persist geometry and its registry so saved entities resolve the same prefab on next launch.
    public sealed partial class FlowerPatchPrefabSystem : GameSystemBase
    {
        public sealed class SavedPatch {
            public string Name;
            public float[] Min, Max;
            public int Vertices, Indices, LodVertices, LodIndices;
        }
        private PrefabSystem _prefabs;
        private StaticObjectPrefab _source;
        private SurfaceAsset _surface;
        private readonly Queue<FlowerPatchMesh> _pending = new Queue<FlowerPatchMesh>();
        private readonly HashSet<string> _requested = new HashSet<string>();
        private readonly Dictionary<string, Entity> _entities = new Dictionary<string, Entity>();
        private readonly Dictionary<string, int> _createdAt = new Dictionary<string, int>();
        private readonly Dictionary<string, SavedPatch> _saved = new Dictionary<string, SavedPatch>();
        private readonly Queue<SavedPatch> _restore = new Queue<SavedPatch>();
        private readonly HashSet<string> _failed = new HashSet<string>();
        private bool _dirty;
        private const string AssetFolder = "ImportedData/FlowerPainter/Patches";
        private static string _storageRoot;
        private static string Manifest => Path.Combine(_storageRoot,AssetFolder,"registry.json");

        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
        private static extern IntPtr CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
        private static extern uint GetFinalPathNameByHandle(IntPtr file,StringBuilder path,uint length,uint flags);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
        private static string PhysicalRoot(string path) {
            if(Environment.OSVersion.Platform!=PlatformID.Win32NT)return path;
            var handle=CreateFile(path,0,7,IntPtr.Zero,3,0x02000000,IntPtr.Zero);
            if(handle==new IntPtr(-1))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            try {
                var result=new StringBuilder(32768);
                uint length=GetFinalPathNameByHandle(handle,result,(uint)result.Capacity,0);
                if(length==0)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                string resolved=result.ToString();return resolved.StartsWith(@"\\?\")?resolved.Substring(4):resolved;
            } finally {CloseHandle(handle);}
        }

        [Preserve] protected override void OnCreate() {
            base.OnCreate(); _prefabs=World.GetOrCreateSystemManaged<PrefabSystem>();
            // The game's LongDirectory helper fails when creating through a Windows junction.
            // Create our owned directory with the standard API before saving native assets.
            _storageRoot=PhysicalRoot(AssetDatabase.user.rootPath);
            Directory.CreateDirectory(Path.Combine(_storageRoot,AssetFolder));
            if(File.Exists(Manifest)) foreach(var saved in JsonConvert.DeserializeObject<List<SavedPatch>>(File.ReadAllText(Manifest))) {
                _saved[saved.Name]=saved;_restore.Enqueue(saved);_requested.Add(saved.Name);
            }
        }
        public bool TryResolve(string name,out Entity entity) {
            entity=Entity.Null;
            return name!=null && _entities.TryGetValue(name,out entity) && UnityEngine.Time.frameCount-_createdAt[name]>=3 && EntityManager.HasComponent<ObjectGeometryData>(entity);
        }
        public bool Prepare(ComposedPlan plan) {
            bool ready=true;
            foreach(var layer in plan.Layers) foreach(var obj in layer.Objects) {
                var mesh=obj.FlowerPatch;if(mesh==null)continue;
                if(_failed.Contains(mesh.Name))throw new InvalidOperationException("Flower patch could not be created: "+mesh.Name);
                if(TryResolve(mesh.Name,out _))continue;
                ready=false;if(_requested.Add(mesh.Name))_pending.Enqueue(mesh);
            }
            return ready;
        }
        [Preserve] protected override void OnUpdate() {
            if(_source==null) {
                if(!_prefabs.TryGetPrefab(new PrefabID(nameof(StaticObjectPrefab),"Flowerbed01"),out var source))return;
                _surface=MakeSurface();_source=(StaticObjectPrefab)source;
            }
            var timer=System.Diagnostics.Stopwatch.StartNew();
            while((_restore.Count>0||_pending.Count>0)&&timer.ElapsedMilliseconds<10) {
                string name="";
                try {
                    if(_restore.Count>0) {var saved=_restore.Dequeue();name=saved.Name;Register(saved);}
                    else {
                        var mesh=_pending.Dequeue();name=mesh.Name;
                        MakeGeometry(name,mesh);MakeGeometry(name+" LOD",mesh.Lod);
                        var min=mesh.Vertices.Aggregate(new float3(float.MaxValue),math.min);
                        var max=mesh.Vertices.Aggregate(new float3(float.MinValue),math.max);
                        var saved=new SavedPatch {Name=name,Min=new[]{min.x,min.y,min.z},Max=new[]{max.x,max.y,max.z},
                            Vertices=mesh.Vertices.Length,Indices=mesh.Indices.Length,LodVertices=mesh.Lod.Vertices.Length,LodIndices=mesh.Lod.Indices.Length};
                        Register(saved);_saved[name]=saved;_dirty=true;
                    }
                } catch(Exception e) {_failed.Add(name);Mod.Log.Error(e,"Flower patch creation failed: "+name);}
            }
            if(_dirty&&_pending.Count==0) {
                Directory.CreateDirectory(Path.GetDirectoryName(Manifest));File.WriteAllText(Manifest,JsonConvert.SerializeObject(_saved.Values));
                _dirty=false;Mod.Log.Info("Flower patch models ready: "+_entities.Count);
            }
        }
        private static Colossal.Hash128 Id(string name)=>Colossal.Hash128.CreateGuid(name);
        private static void MakeGeometry(string name,FlowerPatchMesh data) {
            if(AssetDatabase.global.GetAsset<GeometryAsset>(Id(name))!=null)return;
            var mesh=new Mesh {name=name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.vertices=data.Vertices.Select(v=>new Vector3(v.x,v.y,v.z)).ToArray();
            mesh.uv=data.UV.Select(v=>new Vector2(v.x,v.y)).ToArray();mesh.triangles=data.Indices;
            mesh.tangents=Enumerable.Repeat(new Vector4(1,0,0,1),data.Vertices.Length).ToArray();
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            var asset=AssetDatabase.user.AddAsset<GeometryAsset>(AssetDataPath.Create(AssetFolder,name,EscapeStrategy.None),Id(name));
            asset.SetData(new[]{mesh});asset.Save();
            // The renderer's asynchronous loader expects an unloaded header. SetData populated
            // the import buffers without marking that loader state; release them after saving.
            asset.Unload(true);
        }
        private void Register(SavedPatch saved) {
            var bounds=new Bounds3(new float3(saved.Min[0],saved.Min[1],saved.Min[2]),new float3(saved.Max[0],saved.Max[1],saved.Max[2]));
            RenderPrefab Render(string suffix,int vertices,int indices) {
                var geometry=AssetDatabase.global.GetAsset<GeometryAsset>(Id(saved.Name+suffix));
                if(geometry==null)throw new FileNotFoundException("Flower patch geometry missing: "+saved.Name+suffix);
                var render=ScriptableObject.CreateInstance<RenderPrefab>();render.name=saved.Name+suffix+" mesh";render.geometryAsset=geometry;
                render.surfaceAssets=new[]{_surface};render.bounds=bounds;render.vertexCount=vertices;render.indexCount=indices;render.meshCount=1;render.surfaceArea=16;
                return render;
            }
            var far=Render(" LOD",saved.LodVertices,saved.LodIndices);AssetCatalogSystem.AddOwnPrefab(_prefabs,far);
            var near=Render("",saved.Vertices,saved.Indices);
            // Fine petals need to survive beyond the game's default triangle-density cutoff.
            var lod=near.AddComponent<LodProperties>();lod.m_LodMeshes=new[]{far};lod.m_Bias=-3;lod.m_ShadowBias=1;
            AssetCatalogSystem.AddOwnPrefab(_prefabs,near);
            var prop=ScriptableObject.CreateInstance<StaticObjectPrefab>();prop.name=saved.Name;
            prop.m_Meshes=new[]{new ObjectMeshInfo {m_Mesh=near,m_Position=Vector3.zero,m_Rotation=Quaternion.identity}};
            foreach(var component in _source.components)
                if(component!=null&&!(component is UIObject)&&!(component is ObsoleteIdentifiers))prop.AddComponentFrom(component);
            if(!AssetCatalogSystem.AddOwnPrefab(_prefabs,prop))throw new InvalidOperationException("Cannot register "+prop.name);
            _entities[prop.name]=_prefabs.GetEntity(prop);_createdAt[prop.name]=UnityEngine.Time.frameCount;
        }
        private static SurfaceAsset MakeSurface() {
            const string name="FlowerPainter Patch Atlas v2";
            var existing=AssetDatabase.global.GetAsset<SurfaceAsset>(Id(name));if(existing!=null){existing.LoadProperties(false);return existing;}
            var material=new Material(AssetDatabase.global.resources.materialLibrary.GetMaterial(0)){name=name};
            // The template contains GPU-only default textures. Import only our readable textures.
            foreach(var property in material.GetTexturePropertyNames()) material.SetTexture(property,null);
            var atlas=new Texture2D(128,32,TextureFormat.RGBA32,false){name=name,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[128*32];
            for(int y=0;y<32;y++)for(int x=0;x<128;x++) {
                ColorUtility.TryParseHtmlString("#"+FlowerPatchMesh.Hex[x/8],out var c);float shade=.78f+.075f*(y/8);
                pixels[y*128+x]=new Color(c.r*shade,c.g*shade,c.b*shade,1);
            }
            atlas.SetPixels(pixels);atlas.Apply();
            Texture2D Solid(string label,Color color) {
                var texture=new Texture2D(4,4,TextureFormat.RGBA32,false,true){name=name+label};
                texture.SetPixels(Enumerable.Repeat(color,16).ToArray());texture.Apply();return texture;
            }
            material.SetTexture("_BaseColorMap",atlas);material.SetColor("_BaseColor",Color.white);
            material.SetTexture("_ControlMask",Solid(" control",Color.black));
            material.SetTexture("_MaskMap",Solid(" mask",new Color(0,1,0,.15f)));
            material.SetTexture("_NormalMap",Solid(" normal",new Color(.5f,.5f,1,1)));
            material.SetTexture("_EmissiveColorMap",Solid(" emission",Color.black));
            material.SetColor("_EmissiveColor",Color.black);
            material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.15f);material.SetFloat("_CoatStrength",0);
            material.SetFloat("_UseStack0",0);material.SetFloat("_UseStack1",0);material.DisableKeyword("ENABLE_VT");
            var asset=AssetDatabase.user.AddAsset<SurfaceAsset>(AssetDataPath.Create(AssetFolder,name,EscapeStrategy.None),Id(name));
            asset.SetData(material);
            foreach(var texture in asset.textures.Values) Directory.CreateDirectory(Path.GetDirectoryName(texture.path));
            asset.Save(0,true,true,false);return asset;
        }
    }
}
