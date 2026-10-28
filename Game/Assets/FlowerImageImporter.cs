using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using FlowerPainter.Core.Layers.Types.Floral;

namespace FlowerPainter.Game.Assets
{
    internal static class FlowerImageImporter
    {
        internal sealed class Result { public string Name, Data; public int Width, Height; }

        public static Result Choose(IntPtr owner)
        {
            var dialog=new OpenFileName { Size=Marshal.SizeOf(typeof(OpenFileName)),Owner=owner,
                Filter="Images (PNG, JPEG, BMP, GIF, TIFF, ICO, EMF, WMF)\0*.png;*.jpg;*.jpeg;*.jfif;*.bmp;*.dib;*.gif;*.tif;*.tiff;*.ico;*.emf;*.wmf\0All files\0*.*\0\0",
                File=new StringBuilder(32768),MaxFile=32768,Title="FlowerPainter — Import image / 导入图片",
                Flags=0x00080000|0x00001000|0x00000800|0x00000008 };
            if(!GetOpenFileName(dialog)) {
                int error=CommDlgExtendedError();
                if(error!=0)throw new IOException("File picker failed: "+error);
                return null;
            }
            return Read(dialog.File.ToString());
        }

        internal static Result Read(string path)
        {
            using var source=Image.FromFile(path);
            // Honour camera orientation before deriving aspect ratio. GIF/TIFF use their first frame.
            if(Array.IndexOf(source.PropertyIdList,0x112)>=0) {
                int orientation=BitConverter.ToUInt16(source.GetPropertyItem(0x112).Value,0);
                RotateFlipType[] turns={RotateFlipType.RotateNoneFlipNone,RotateFlipType.RotateNoneFlipNone,
                    RotateFlipType.RotateNoneFlipX,RotateFlipType.Rotate180FlipNone,RotateFlipType.Rotate180FlipX,
                    RotateFlipType.Rotate90FlipX,RotateFlipType.Rotate90FlipNone,RotateFlipType.Rotate270FlipX,RotateFlipType.Rotate270FlipNone};
                if(orientation>=1 && orientation<=8)source.RotateFlip(turns[orientation]);
            }
            int ow=source.Width,oh=source.Height;
            double scale=Math.Min(1,FlowerImage.MaxSide/(double)Math.Max(ow,oh));
            int w=Math.Max(1,(int)Math.Round(ow*scale)),h=Math.Max(1,(int)Math.Round(oh*scale));
            using var bitmap=new Bitmap(w,h,PixelFormat.Format32bppArgb);
            using(var graphics=Graphics.FromImage(bitmap)) {
                graphics.CompositingMode=CompositingMode.SourceCopy;
                graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(source,new Rectangle(0,0,w,h),0,0,ow,oh,GraphicsUnit.Pixel);
            }
            var colors=new byte[w*h];var row=new byte[w*4];
            var bits=bitmap.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try {
                for(int y=0;y<h;y++) {
                    Marshal.Copy(IntPtr.Add(bits.Scan0,y*bits.Stride),row,0,row.Length);
                    for(int x=0;x<w;x++)colors[y*w+x]=FlowerImage.Nearest(row[4*x+2],row[4*x+1],row[4*x],row[4*x+3]);
                }
            } finally {bitmap.UnlockBits(bits);}
            string data=FlowerImage.Encode(ow,oh,w,h,colors);
            FlowerImage.Read(data); // Reject completely transparent images before replacing a design.
            return new Result {Name=Path.GetFileName(path),Data=data,Width=ow,Height=oh};
        }

        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
        private sealed class OpenFileName
        {
            public int Size; public IntPtr Owner,Instance; public string Filter; public IntPtr CustomFilter;
            public int MaxCustomFilter,FilterIndex; public StringBuilder File; public int MaxFile;
            public IntPtr FileTitle; public int MaxFileTitle; public string InitialDir,Title; public int Flags;
            public short FileOffset,FileExtension; public string DefaultExtension; public IntPtr CustomData,Hook,TemplateName,Reserved;
            public int ReservedInt,FlagsEx;
        }
        [DllImport("comdlg32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
        [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetOpenFileName([In,Out] OpenFileName dialog);
        [DllImport("comdlg32.dll")] private static extern int CommDlgExtendedError();
    }
}
