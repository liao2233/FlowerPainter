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
        public const int MaterialVersion = 5;
        public const int TilePitches = 64;
        public float3[] Vertices;
        public float2[] UV;
        public int[] Indices;
        public FlowerPatchMesh Lod;
        // Actual raised detail flowers; dense coverage comes from the shared texture.
        public int Flowers;
        public string Name;
        public static readonly string[] Hex = { "193D17", "326329", "487F32", "E3B832", "D8202F", "FFD338", "2352B4", "F5F0E4", "F288B2", "78399D", "F17D25", "B72A74", "201D2D", "168242", "48B5CD", "182D68" };
        public static int ColorOf(string asset)
        {
            string s=asset??"";
            string[] names={"Red","Yellow","Blue","White","Pink","Purple","Orange","Magenta","Black","Green","Cyan","Navy"};
            for(int i=0;i<names.Length;i++)if(s.IndexOf(names[i],StringComparison.OrdinalIgnoreCase)>=0)return i+4;
            return s.Contains("02")?5:8;
        }
        internal sealed class Builder
        {
            private readonly List<float3> _v=new List<float3>();
            private readonly List<float2> _uv=new List<float2>();
            private readonly List<int> _i=new List<int>();
            public void Triangle(float3 a,float3 b,float3 c,float2 ua,float2 ub,float2 uc)
            {
                if(math.cross(b-a,c-a).y<0){var p=b;b=c;c=p;var u=ub;ub=uc;uc=u;}
                int start=_v.Count;_v.Add(a);_v.Add(b);_v.Add(c);_uv.Add(ua);_uv.Add(ub);_uv.Add(uc);
                _i.Add(start);_i.Add(start+1);_i.Add(start+2);
            }
            public void Top(float3 a,float3 b,float3 c,int color,int shade=2)
            {
                var uv=new float2((color+.5f)/16f,.5f+(shade+.5f)/8f);
                Triangle(a,b,c,uv,uv,uv);
            }
            public FlowerPatchMesh Finish()=>new FlowerPatchMesh {Vertices=_v.ToArray(),UV=_uv.ToArray(),Indices=_i.ToArray()};
        }
        internal static FlowerPatchMesh Make(List<Region> pieces,float3 origin,float pitch,float height,
            Func<float2,int> colorAt,Func<float2,float> terrain,Func<float2,float> clearance,System.Threading.CancellationToken cancel,
            Dictionary<string,FlowerPatchMesh> cache)
        {
            var near=new Builder();var far=new Builder();int flowers=0;
            var originCell=(int2)math.round(origin.xz/pitch);
            float3 Ground(float2 p,float up) {
                var index=(int2)math.round(p/pitch);
                var local=math.select(p-origin.xz,(float2)(index-originCell)*pitch,math.abs(p-(float2)index*pitch)<.002f);
                return new float3((float)Math.Round(local.x,3),(float)Math.Round(terrain(p)-origin.y+up,3),(float)Math.Round(local.y,3));
            }
            foreach(var region in pieces) {
                Poly.Bounds(region.Outer,out var min,out var max);
                void Cell(Builder target,float2 lo,float size,bool distant)
                {
                    cancel.ThrowIfCancellationRequested();
                    var mid=lo+size*.5f;bool inside=region.Contains(mid);
                    bool whole=region.DistanceToBoundary(mid)>size*.707107f+.002f;
                    if(whole&&!inside)return;
                    var sampleOrigin=(int2)math.round(lo/pitch);
                    int n=math.max(1,(int)math.round(size/pitch));
                    int Sample(int x,int z)=>colorAt(new float2(sampleOrigin.x+x+.5f,sampleOrigin.y+z+.5f)*pitch);
                    int color=Sample(0,0);
                    bool uniform=true;
                    // Keep the original colour sampling at boundaries, including small flag emblems.
                    for(int z=0;z<n&&uniform;z++)for(int x=0;x<n;x++)
                        if(Sample(x,z)!=color){uniform=false;break;}
                    float2 a=lo,b=lo+new float2(size,0),c=lo+size,d=lo+new float2(0,size);
                    float ha=terrain(a),hb=terrain(b),hc=terrain(c),hd=terrain(d);
                    float error=math.abs(terrain(mid)-(ha+hc)*.5f);
                    error=math.max(error,math.abs(terrain((a+b)*.5f)-(ha+hb)*.5f));
                    error=math.max(error,math.abs(terrain((b+c)*.5f)-(hb+hc)*.5f));
                    error=math.max(error,math.abs(terrain((c+d)*.5f)-(hc+hd)*.5f));
                    error=math.max(error,math.abs(terrain((d+a)*.5f)-(hd+ha)*.5f));
                    if(n>1&&(!uniform||(!whole&&color<0)||error>height*.12f)) {
                        float half=size*.5f;
                        Cell(target,lo,half,distant);Cell(target,lo+new float2(half,0),half,distant);
                        Cell(target,lo+new float2(0,half),half,distant);Cell(target,lo+half,half,distant);return;
                    }
                    if(color<0&&whole)return;
                    float up=height*.72f;
                    float2 UV(float2 p,int tint) {
                        var q=math.saturate((p-lo)/size);
                        q=math.select(q,0,q<.001f);q=math.select(q,1,q>.999f);
                        // Four density variants per colour, with 32 px gutters retained through all mips.
                        return new float2((tint*4+math.min(n,4)-1+(32.5f+q.x*63)/128)/64,(32.5f+q.y*63)/256);
                    }
                    void Triangle(float2 p,float2 q,float2 r) {
                        int tint=color>=0?color:colorAt((p+q+r)/3);if(tint<0)return;
                        if(distant) {
                            var uv=new float2((tint+.5f)/16,.984375f);
                            target.Triangle(Ground(p,up),Ground(q,up),Ground(r,up),uv,uv,uv);
                        }
                        else target.Triangle(Ground(p,up),Ground(q,up),Ground(r,up),UV(p,tint),UV(q,tint),UV(r,tint));
                    }
                    if(whole) {Triangle(a,b,c);Triangle(a,c,d);return;}
                    // Only border cells need polygon clipping. Curves and holes stay exact.
                    var clipped=Poly.Boolean(new[]{region},new[]{new Region(new[]{a,b,c,d})},BoolOp.Intersection);
                    foreach(var part in clipped)if(EarClip.Triangulate(part,out var verts,out var triangles))
                        foreach(var t in triangles)Triangle(verts[t.x],verts[t.y],verts[t.z]);
                }
                void Cover(Builder target,int multiplier,bool distant) {
                    float cell=pitch*multiplier;
                    for(int z=(int)math.floor(min.y/cell+.001f);z*cell<max.y-.001f;z++)
                        for(int x=(int)math.floor(min.x/cell+.001f);x*cell<max.x-.001f;x++)Cell(target,new float2(x*cell,z*cell),cell,distant);
                }
                Cover(near,4,false);Cover(far,8,true);
                // One raised detail per 4 x 4 canopy cells, not one model for every printed blossom.
                float detailStep=16*pitch;
                for(int z=(int)math.floor(min.y/detailStep);z*detailStep<max.y;z++)for(int x=(int)math.floor(min.x/detailStep);x*detailStep<max.x;x++) {
                    cancel.ThrowIfCancellationRequested();uint seed=Hashing.Mix(1987u,(x%4+4)%4,(z%4+4)%4);
                    var local=(new float2(x+.5f,z+.5f)*16-originCell)*pitch;
                    local+=new float2((seed&255)/255f-.5f,((seed>>8)&255)/255f-.5f)*detailStep*.5f;
                    var p=origin.xz+local;
                    if(!region.Contains(p))continue;
                    float radius=math.min(pitch*.6f,clearance(p)/1.03f);int color=colorAt(p);
                    if(radius<.025f||color<0)continue;
                    var center=Ground(p,height*1.02f);center.x=(float)Math.Round(local.x,3);center.z=(float)Math.Round(local.y,3);
                    float rotation=(seed%360)*math.PI/180;
                    for(int k=0;k<5;k++) {
                        float angle=rotation+k*math.PI*.4f;
                        var left=center+new float3(math.cos(angle-.5f)*radius,-height*.08f,math.sin(angle-.5f)*radius);
                        var right=center+new float3(math.cos(angle+.5f)*radius,-height*.08f,math.sin(angle+.5f)*radius);
                        near.Top(center,left,right,color,1+k%3);
                    }
                    float core=radius*.16f;
                    near.Top(center+new float3(-core,.008f,-core),center+new float3(core,.008f,-core),center+new float3(0,.012f,core),3);
                    flowers++;
                }
            }
            var mesh=near.Finish();mesh.Lod=far.Finish();mesh.Flowers=flowers;
            using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);
            writer.Write(5);writer.Write(MaterialVersion);
            foreach(var part in new[]{mesh,mesh.Lod}) {
                writer.Write(part.Vertices.Length);
                for(int i=0;i<part.Vertices.Length;i++) {
                    var v=part.Vertices[i];var uv=part.UV[i];
                    writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);writer.Write(uv.x);writer.Write(uv.y);
                }
            }
            using var hash=SHA256.Create();mesh.Name=Prefix+BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").Substring(0,32);
            if(cache.TryGetValue(mesh.Name,out var shared))return shared;
            cache.Add(mesh.Name,mesh);return mesh;
        }
    }
}
