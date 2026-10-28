using System;
using System.IO;
using System.Linq;
using System.Threading;
using FlowerPainter.Core;
using FlowerPainter.Core.Build;
using FlowerPainter.Core.Layers.Types.Floral;
using FlowerPainter.Core.Tool;
using Unity.Mathematics;
using Xunit;

namespace FlowerPainter.Tests
{
    public class FloralImageTests
    {
        private static string Image()=>FlowerImage.Encode(400,200,2,2,new byte[]{4,5,6,255});

        [Fact] public void ColoursAndTransparencyMatchFlowerPalette() {
            for(int i=4;i<FlowerPatchMesh.Hex.Length;i++) {
                var h=FlowerPatchMesh.Hex[i];
                Assert.Equal(i,FlowerImage.Nearest(Convert.ToByte(h.Substring(0,2),16),Convert.ToByte(h.Substring(2,2),16),Convert.ToByte(h.Substring(4,2),16),255));
            }
            Assert.Equal(255,FlowerImage.Nearest(255,0,0,127));
        }
        [Fact] public void SourceAspectAndImageTopArePreserved() {
            var map=FlowerImage.Read(Image());Assert.Equal(2,map.Ratio);
            Assert.Equal(4,map.Color(new float2(.25f,.75f)));
            Assert.Equal(5,map.Color(new float2(.75f,.75f)));
            Assert.Equal(6,map.Color(new float2(.25f,.25f)));
            Assert.Equal(-1,map.Color(new float2(.75f,.25f)));
            Assert.Equal(-1,map.Color(new float2(1.01f,.5f)));
        }
        [Fact] public void LargeUniformImagesAndRecipeRoundTripNeedNoSourceFile() {
            var data=FlowerImage.Encode(4096,4096,512,512,Enumerable.Repeat((byte)4,512*512).ToArray());
            Assert.Equal(4,FlowerImage.Read(data).Color(new float2(.5f)));
            var recipe=new Recipe {Id="picture",Root=new RecipeStep {Layer="floral",Settings=new FloralLayer().Defaults()}};
            recipe.Root.Settings.Values["shape"]="image";recipe.Root.Settings.Values["imageData"]=data;
            var copy=RecipeJson.Read(RecipeJson.Write(recipe));
            Assert.Equal(data,copy.Recipe.Root.Settings.S("imageData",""));
            Assert.True(FlowerImage.IsReady(copy.Recipe.Root.Settings));
        }
        [Fact] public void EmptyOrCorruptImagesCannotBuild() {
            var layer=new FloralLayer();var settings=layer.Defaults();settings.Values["shape"]="image";
            Assert.False(layer.CheckReady(new AreaInput {Settings=settings}).Ready);
            settings.Values["imageData"]="invalid";Assert.False(FlowerImage.IsReady(settings));
            Assert.Throws<InvalidDataException>(()=>FlowerImage.Read(FlowerImage.Encode(2,2,2,2,new byte[]{255,255,255,255})));
            var bytes=Convert.FromBase64String(Image());Array.Resize(ref bytes,bytes.Length-1);
            Assert.ThrowsAny<IOException>(()=>FlowerImage.Read(Convert.ToBase64String(bytes)));
            Assert.Throws<InvalidDataException>(()=>FlowerImage.Encode(2,2,2,2,new byte[]{99,4,4,4}));
        }
        [Fact] public void DetailedImageSurvivesBinaryGroupReceiptAndRemainsEditable() {
            int side=FlowerImage.MaxSide;
            var pixels=Enumerable.Range(0,side*side).Select(i=>(byte)(4+i%12)).ToArray();
            var data=FlowerImage.Encode(4096,4096,side,side,pixels);
            Assert.True(data.Length>1<<20);
            var recipe=new Recipe {Root=new RecipeStep {Layer="floral",Settings=new FloralLayer().Defaults()}};
            recipe.Root.Settings.Values["shape"]="image";recipe.Root.Settings.Values["imageData"]=data;
            var result=ReceiptCodec.Decode(ReceiptCodec.Encode(new GroupRecord {GroupId=1,Recipe=recipe}));
            Assert.NotNull(result.Recipe);
            Assert.Equal(data,result.Recipe.Root.Settings.S("imageData",""));
            Assert.True(FlowerImage.IsReady(result.Recipe.Root.Settings));
        }
        [Theory][InlineData(0,20,0,0)][InlineData(45,40,80,120)][InlineData(90,10,-30,15)]
        public void PlantedImageCanMoveRotateScaleAndKeepsTransparentAreaEmpty(float angle,float width,float x,float z) {
            var layer=new FloralLayer();var settings=layer.Defaults();
            settings.Values["shape"]="image";settings.Values["imageData"]=Image();
            settings.Values["flagDirect"]=true;settings.Values["flagAngle"]=angle;settings.Values["flagWidth"]=width;
            float height=width/2;var center=new float2(x,z);
            math.sincos(math.radians(angle),out float sn,out float cs);
            float2 Rot(float2 p)=>new float2(p.x*cs-p.y*sn,p.x*sn+p.y*cs);
            float2 Unrot(float2 p)=>new float2(p.x*cs+p.y*sn,-p.x*sn+p.y*cs);
            float3 P(float2 p)=>new float3(p.x,0,p.y);
            var ring=new[]{P(center+Rot(new float2(-width,-height)*.5f)),P(center+Rot(new float2(width,-height)*.5f)),P(center+Rot(new float2(width,height)*.5f)),P(center+Rot(new float2(-width,height)*.5f))};
            var plan=layer.Plan(new AreaInput {Settings=settings,Seed=42,Site=new SiteContext {HeightAt=p=>0},Shape=new AreaShape {Ring=ring}},CancellationToken.None);
            Assert.NotEmpty(plan.Objects);var map=FlowerImage.Read(Image());
            foreach(var obj in plan.Objects) {
                var mesh=obj.FlowerPatch;Assert.NotEmpty(mesh.Lod.Vertices);
                Assert.Equal(mesh.Flowers*18,mesh.Indices.Length);
                for(int i=0;i<mesh.Indices.Length;i+=18) {
                    int v=mesh.Indices[i];var uv=Unrot((mesh.Vertices[v]+obj.Position).xz-center)/new float2(width,height)+.5f;
                    int expected=map.Color(uv);Assert.True(expected>=0);Assert.Equal(expected,(int)(mesh.UV[v].x*16));
                }
            }
        }
    }
}
