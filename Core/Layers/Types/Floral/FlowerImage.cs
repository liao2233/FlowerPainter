using System;
using System.IO;
using Unity.Mathematics;

namespace FlowerPainter.Core.Layers.Types.Floral
{
    // Indexed flower colours, kept inside the recipe so edits never depend on the source file.
    internal static class FlowerImage
    {
        public const int MaxSide = 1024;
        public const int MaxEncodedLength = (20+MaxSide*MaxSide*3+2)/3*4;
        private static readonly object Gate = new object();
        private static string _encoded;
        private static FlagTemplate _template;
        private static readonly int3[] Palette = Array.ConvertAll(FlowerPatchMesh.Hex,
            s => new int3(Convert.ToInt32(s.Substring(0,2),16),Convert.ToInt32(s.Substring(2,2),16),Convert.ToInt32(s.Substring(4,2),16)));

        public static byte Nearest(byte r, byte g, byte b, byte a)
        {
            if (a < 128) return 255;
            int best=4; double distance=double.MaxValue;
            for(int i=4;i<Palette.Length;i++) {
                var d=Palette[i]-new int3(r,g,b);
                double mean=(Palette[i].x+r)*.5;
                double score=(2+mean/256)*d.x*d.x+4*d.y*d.y+(2+(255-mean)/256)*d.z*d.z;
                if(score<distance) {distance=score;best=i;}
            }
            return (byte)best;
        }

        public static string Encode(int originalWidth,int originalHeight,int width,int height,byte[] pixels)
        {
            Validate(originalWidth,originalHeight,width,height);
            if(pixels==null || pixels.Length!=width*height) throw new InvalidDataException("Invalid image pixels.");
            using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);
            writer.Write(1);writer.Write(originalWidth);writer.Write(originalHeight);writer.Write(width);writer.Write(height);
            for(int i=0;i<pixels.Length;) {
                byte color=pixels[i];ValidateColor(color);
                int end=i+1;while(end<pixels.Length && pixels[end]==color && end-i<ushort.MaxValue)end++;
                writer.Write((ushort)(end-i));writer.Write(color);i=end;
            }
            writer.Flush();return Convert.ToBase64String(stream.ToArray());
        }

        public static FlagTemplate Read(string encoded)
        {
            lock(Gate) {
                if(encoded!=null && encoded==_encoded)return _template;
                if(string.IsNullOrEmpty(encoded) || encoded.Length>MaxEncodedLength)throw new InvalidDataException("Missing or invalid flower image.");
                using var stream=new MemoryStream(Convert.FromBase64String(encoded));using var reader=new BinaryReader(stream);
                if(reader.ReadInt32()!=1)throw new InvalidDataException("Unsupported flower image version.");
                int ow=reader.ReadInt32(),oh=reader.ReadInt32(),w=reader.ReadInt32(),h=reader.ReadInt32();
                Validate(ow,oh,w,h);var pixels=new byte[w*h];bool painted=false;
                for(int p=0;p<pixels.Length;) {
                    int n=reader.ReadUInt16();byte color=reader.ReadByte();ValidateColor(color);
                    if(n==0||n>pixels.Length-p)throw new InvalidDataException("Invalid flower image run.");
                    for(int end=p+n;p<end;p++)pixels[p]=color;
                    painted|=color!=255;
                }
                if(stream.Position!=stream.Length || !painted)throw new InvalidDataException("The image contains no visible flowers or invalid data.");
                var result=new FlagTemplate(w,h,pixels) {Id="image",Ratio=(float)ow/oh,RecommendedColumns=w,Outline=Array.Empty<float2>()};
                _encoded=encoded;_template=result;return result;
            }
        }

        public static bool IsReady(SettingsBag settings)
        {
            try {Read(settings.S("imageData",""));return true;}
            catch(Exception e) when(e is IOException || e is InvalidDataException || e is FormatException || e is ArgumentException) {return false;}
        }
        public static FlagTemplate For(SettingsBag settings)=>settings.S("shape","")=="image"
            ?Read(settings.S("imageData","")):FlagCatalog.Get(settings.S("flagTemplate","china"));
        private static void Validate(int ow,int oh,int w,int h) {
            if(ow<1||oh<1||w<1||h<1||w>MaxSide||h>MaxSide)throw new InvalidDataException("Invalid image dimensions.");
        }
        private static void ValidateColor(byte color) {
            if(color!=255 && (color<4||color>=Palette.Length))throw new InvalidDataException("Invalid flower colour.");
        }
    }
}
