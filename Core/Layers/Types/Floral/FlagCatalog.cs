using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlowerPainter.Core.Tool;
using Unity.Mathematics;

namespace FlowerPainter.Core.Layers.Types.Floral
{
    internal sealed class FlagTemplate
    {
        public string Id;
        public float Ratio;
        public int RecommendedColumns;
        public float2[] Outline;
        private readonly Lazy<(int width,int height,byte[] pixels)> _map;
        public FlagTemplate() { _map=new Lazy<(int,int,byte[])>(ReadMap); }
        private (int,int,byte[]) ReadMap()
        {
            using var stream=FlagCatalog.Resource("maps."+Id+".bin");
            using var reader=new BinaryReader(stream);
            int w=reader.ReadUInt16(),h=reader.ReadUInt16();var pixels=new byte[w*h];
            for(int p=0;p<pixels.Length;) {
                int count=reader.ReadUInt16();byte color=reader.ReadByte();
                if(count==0||count>pixels.Length-p)throw new InvalidDataException("Invalid flag map: "+Id);
                for(int end=p+count;p<end;p++)pixels[p]=color;
            }
            return(w,h,pixels);
        }
        public int Color(float2 uv)
        {
            if(math.any(uv<0)||math.any(uv>1))return -1;
            var m=_map.Value;
            int x=math.min((int)(uv.x*m.width),m.width-1),y=math.min((int)((1-uv.y)*m.height),m.height-1);
            int color=m.pixels[y*m.width+x];return color==255?-1:color;
        }
        public float2 Fit(float2 span) {float width=math.min(span.x,span.y*Ratio);return new float2(width,width/Ratio);}
    }

    internal static class FlagCatalog
    {
        internal static Stream Resource(string suffix)=>typeof(FlagCatalog).Assembly.GetManifestResourceStream("FlowerPainter.Flags."+suffix)
            ?? throw new InvalidDataException("Missing embedded flag data: "+suffix);
        public static readonly IReadOnlyList<FlagTemplate> All=Read();
        private static readonly Dictionary<string,FlagTemplate> ById=All.ToDictionary(f=>f.Id,StringComparer.Ordinal);
        public static FlagTemplate Get(string id)=>ById.TryGetValue(id??"",out var flag)?flag:ById["china"];
        private static FlagTemplate[] Read()
        {
            using var stream=Resource("catalog.json");using var reader=new StreamReader(stream);
            return JsonValue.Parse(reader.ReadToEnd()).Items.Select(j=>new FlagTemplate {
                Id=j["id"].AsString(), Ratio=(float)j["ratio"].AsNumber(), RecommendedColumns=(int)j["columns"].AsNumber(),
                Outline=j["outline"].Items.Select(p=>new float2((float)p.Items[0].AsNumber(),(float)p.Items[1].AsNumber())).ToArray()
            }).ToArray();
        }
    }
}
