using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using FlowerPainter.Core.Geometry;
using FlowerPainter.Core.Layers.Common;
using Unity.Mathematics;

namespace FlowerPainter.Core.Layers.Types.Floral
{
    public sealed class FlowerPatchMesh
    {
        public const string Prefix = "FlowerPainter Patch ";
        public float3[] Vertices;
        public float2[] UV;
        public int[] Indices;
        public FlowerPatchMesh Lod;
        public int Flowers;
        public string Name;

        public void Identify()
        {
            Vertices = Array.ConvertAll(Vertices, p => math.round(p*1000)/1000);
            if (Lod != null) Lod.Vertices = Array.ConvertAll(Lod.Vertices, p => math.round(p*1000)/1000);
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            writer.Write(2); Write(this); if (Lod != null) Write(Lod);
            using var hash = SHA256.Create();
            Name = Prefix + BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-", "").Substring(0, 32);
            void Write(FlowerPatchMesh mesh) {
                foreach (var p in mesh.Vertices) { writer.Write(p.x); writer.Write(p.y); writer.Write(p.z); }
                foreach (var uv in mesh.UV) { writer.Write(uv.x); writer.Write(uv.y); }
                foreach (var i in mesh.Indices) writer.Write(i);
            }
        }

        public static readonly string[] Hex = { "193D17", "326329", "487F32", "E3B832", "D8202F", "FFD338", "2352B4", "F5F0E4", "F288B2", "78399D", "F17D25", "B72A74", "201D2D", "168242", "48B5CD", "182D68" };
        public static int ColorOf(string asset)
        {
            string s = asset ?? "";
            string[] names = { "Red", "Yellow", "Blue", "White", "Pink", "Purple", "Orange", "Magenta", "Black", "Green", "Cyan", "Navy" };
            for (int i = 0; i < names.Length; i++) if (s.IndexOf(names[i], StringComparison.OrdinalIgnoreCase) >= 0) return i + 4;
            if (s.Contains("02")) return 5;
            return 8;
        }

        internal sealed class Builder
        {
            private readonly List<float3> _v = new List<float3>();
            private readonly List<float2> _uv = new List<float2>();
            private readonly List<int> _i = new List<int>();
            public void Triangle(float3 a, float3 b, float3 c, int color, int shade = 2)
            {
                int start = _v.Count;
                _v.Add(a); _v.Add(b); _v.Add(c);
                var uv = new float2((color + 0.5f) / 16f, (shade + 0.5f) / 4f);
                _uv.Add(uv); _uv.Add(uv); _uv.Add(uv);
                _i.Add(start); _i.Add(start + 1); _i.Add(start + 2);
            }
            public void Top(float3 a, float3 b, float3 c, int color, int shade = 2)
            {
                if (math.cross(b-a,c-a).y < 0) Triangle(a,c,b,color,shade); else Triangle(a,b,c,color,shade);
            }
            public FlowerPatchMesh Finish() => new FlowerPatchMesh { Vertices=_v.ToArray(), UV=_uv.ToArray(), Indices=_i.ToArray() };
        }

        internal static FlowerPatchMesh Make(List<Region> pieces, float3 origin, float pitch, float height,
            Func<float2,int> colorAt, Func<float2,float> terrain, Func<float2,float> clearance, System.Threading.CancellationToken cancel)
        {
            var near = new Builder(); var far = new Builder(); int flowers = 0;
            float3 Ground(float2 p, float up=0) => new float3((float)Math.Round(p.x-origin.x,3), (float)Math.Round(terrain(p)-origin.y,3)+up, (float)Math.Round(p.y-origin.z,3));
            foreach (var region in pieces) {
                cancel.ThrowIfCancellationRequested();
                if (!EarClip.Triangulate(region, out var verts, out var tris)) continue;
                foreach (var t in tris) {
                    var a=Ground(verts[t.x],0.04f); var b=Ground(verts[t.y],0.04f); var c=Ground(verts[t.z],0.04f);
                    near.Top(a,b,c,0); far.Top(a,b,c,0);
                }
                Poly.Bounds(region.Outer,out var min,out var max);
                int x0=(int)math.floor(min.x/pitch), x1=(int)math.ceil(max.x/pitch);
                int z0=(int)math.floor(min.y/pitch), z1=(int)math.ceil(max.y/pitch);
                for(int z=z0;z<z1;z++) for(int x=x0;x<x1;x++) {
                    uint seed=Hashing.Mix(1987u,x%14,z%14);
                    var p=new float2((x+0.5f)*pitch,(z+0.5f)*pitch);
                    // Keep ownership on a single tile. Petals may touch adjacent tiles without duplicate stems.
                    if(!region.Contains(p))continue;
                    var jitter=new float2((seed&255)/255f-.5f,((seed>>8)&255)/255f-.5f)*pitch*.26f;
                    if(clearance(p)>math.length(jitter))p+=jitter;
                    float radius=math.min(pitch*0.64f,clearance(p)/1.03f);
                    if(radius<0.025f)continue;
                    int color=colorAt(p); if(color<0)continue;
                    float rotation=(seed%360)*math.PI/180f;
                    float h=height*(0.85f+(seed%31)/100f);
                    var ground=Ground(p); var center=ground+new float3(0,h,0);
                    float3 Rad(float angle,float r,float y) => ground+new float3(math.cos(angle)*r,y,math.sin(angle)*r);
                    // Low leaves form a textured green mass beneath the flower heads.
                    for(int leaf=0;leaf<3;leaf++) {
                        float angle=rotation+leaf*2.094395f;
                        var tip=Rad(angle,radius*1.03f,h*0.42f);
                        var left=Rad(angle-0.5f,radius*0.55f,h*0.22f);
                        var right=Rad(angle+0.5f,radius*0.55f,h*0.22f);
                        var basePoint=ground+new float3(0,h*0.08f,0);
                        near.Top(basePoint,left,tip,1,leaf%3);near.Top(basePoint,tip,right,2,leaf%3);
                    }
                    // Five cupped petals; all flowers in this tile are one mesh/entity.
                    for(int petal=0;petal<5;petal++) {
                        float a=rotation+petal*math.PI*0.4f;
                        var left=Rad(a-0.52f,radius*0.82f,h*0.94f);
                        var tipLeft=Rad(a-0.24f,radius,h);
                        var tipRight=Rad(a+0.24f,radius,h);
                        var right=Rad(a+0.52f,radius*0.82f,h*0.94f);
                        near.Top(center,left,tipLeft,color,1+(int)(seed%3));
                        near.Top(center,tipLeft,tipRight,color,2);near.Top(center,tipRight,right,color,2);
                    }
                    float core=radius*0.15f;
                    near.Top(center+new float3(-core,0.006f,-core),center+new float3(core,0.006f,-core),center+new float3(0,0.018f,core),3);
                    // An octagon approximates the same flower-head coverage at distance.
                    for(int k=0;k<8;k++) far.Top(center,Rad(rotation+k*math.PI/4,radius*.9f,h*.98f),Rad(rotation+(k+1)*math.PI/4,radius*.9f,h*.98f),color);
                    flowers++;
                }
            }
            var mesh=near.Finish();mesh.Lod=far.Finish();mesh.Flowers=flowers;mesh.Identify();return mesh;
        }
    }
}
