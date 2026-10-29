using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using FlowerPainter.Core.Layers.Types.Floral;

namespace FlowerPainter.Game.Assets
{
    internal static class FlowerImageImporter
    {
        internal sealed class Result { public string Name, Data; public int Width, Height; }

        public static Result Choose()
        {
            // Keep the Windows structure blittable. Mono cannot reliably marshal a StringBuilder
            // field from a mod loaded in memory into OPENFILENAMEW.
            var dialog=new OpenFileName {Size=Marshal.SizeOf(typeof(OpenFileName)),MaxFile=32768,
                Flags=0x00080000|0x00001000|0x00000800|0x00000008};
            var module=LoadLibrary(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"comdlg32.dll"));
            if(module==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            string selected=null;
            try {
                dialog.Filter=Marshal.StringToHGlobalUni("Images (PNG, JPEG, BMP, GIF, TIFF, ICO, EMF, WMF)\0*.png;*.jpg;*.jpeg;*.jfif;*.bmp;*.dib;*.gif;*.tif;*.tiff;*.ico;*.emf;*.wmf\0All files\0*.*\0\0");
                dialog.Title=Marshal.StringToHGlobalUni("FlowerPainter — Import image / 导入图片");
                dialog.File=Marshal.AllocHGlobal(dialog.MaxFile*2);Marshal.WriteInt16(dialog.File,0);
                var open=Export<OpenDialog>(module,"GetOpenFileNameW");
                Mod.Log.Info("FlowerPainter: image file picker ready");
                if(!open(ref dialog)) {
                    int error=Export<DialogError>(module,"CommDlgExtendedError")();
                    if(error!=0)throw new IOException("File picker failed: "+error);
                    return null;
                }
                selected=Marshal.PtrToStringUni(dialog.File);
            } finally {
                Marshal.FreeHGlobal(dialog.Filter);Marshal.FreeHGlobal(dialog.Title);Marshal.FreeHGlobal(dialog.File);
                FreeLibrary(module);
            }
            return Read(selected);
        }

        internal static Result Read(string path)
        {
            using var source=Image.FromFile(path,true);
            return Read(source,Path.GetFileName(path));
        }

        public static Result Paste()
        {
            byte[] png=null;Image bitmap=null;string file=null;
            if(!OpenClipboard(IntPtr.Zero))throw new InvalidOperationException("FlowerPainter.UI.Images.ClipboardBusy");
            try {
                // Read only the requested native format. Mono's Windows.Forms clipboard decoder can crash on DIBs.
                foreach(var format in new[]{"PNG","image/png"}) {
                    uint id=RegisterClipboardFormat(format);
                    var handle=id==0?IntPtr.Zero:GetClipboardData(id);
                    if(handle!=IntPtr.Zero) {
                        int size=checked((int)GlobalSize(handle).ToUInt64());
                        if(size==0)throw new InvalidDataException("Clipboard PNG is empty");
                        var memory=GlobalLock(handle);
                        if(memory==IntPtr.Zero)throw new InvalidOperationException("FlowerPainter.UI.Images.ClipboardBusy");
                        try{png=new byte[size];Marshal.Copy(memory,png,0,size);}finally{GlobalUnlock(handle);}
                        break;
                    }
                }
                if(png==null) {
                    var handle=GetClipboardData(2); // CF_BITMAP; Windows also synthesizes this from CF_DIB.
                    if(handle!=IntPtr.Zero)bitmap=Image.FromHbitmap(handle);
                    else {
                        var drop=GetClipboardData(15); // CF_HDROP: an image file copied in Explorer.
                        if(drop!=IntPtr.Zero) {
                            uint length=DragQueryFile(drop,0,null,0);
                            if(length>0){var path=new System.Text.StringBuilder(checked((int)length+1));DragQueryFile(drop,0,path,(uint)path.Capacity);file=path.ToString();}
                        }
                    }
                }
            } finally {CloseClipboard();}
            if(png!=null){using var input=new MemoryStream(png);using var source=Image.FromStream(input);return Read(source,"Clipboard.png");}
            if(bitmap!=null){using(bitmap)return Read(bitmap,"Clipboard.png");}
            if(file!=null)return Read(file);
            throw new InvalidOperationException("FlowerPainter.UI.Images.NoClipboardImage");
        }

        private static Result Read(Image source,string name)
        {
            // Honour camera orientation before deriving aspect ratio. GIF/TIFF use their first frame.
            if(Array.IndexOf(source.PropertyIdList,0x112)>=0) {
                var orientationBytes=source.GetPropertyItem(0x112)?.Value;
                int orientation=orientationBytes!=null && orientationBytes.Length>=2 ? BitConverter.ToUInt16(orientationBytes,0) : 1;
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
            return new Result {Name=name,Data=data,Width=ow,Height=oh};
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct OpenFileName
        {
            public int Size; public IntPtr Owner,Instance; public IntPtr Filter; public IntPtr CustomFilter;
            public int MaxCustomFilter,FilterIndex; public IntPtr File; public int MaxFile;
            public IntPtr FileTitle; public int MaxFileTitle; public IntPtr InitialDir,Title; public int Flags;
            public short FileOffset,FileExtension; public IntPtr DefaultExtension; public IntPtr CustomData,Hook,TemplateName,Reserved;
            public int ReservedInt,FlagsEx;
        }
        private static T Export<T>(IntPtr module,string name) where T:Delegate {
            var address=GetProcAddress(module,name);
            if(address==IntPtr.Zero)throw new EntryPointNotFoundException(name);
            return (T)Marshal.GetDelegateForFunctionPointer(address,typeof(T));
        }
        [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool OpenClipboard(IntPtr owner);
        [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool CloseClipboard();
        [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint format);
        [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="RegisterClipboardFormatW")] private static extern uint RegisterClipboardFormat(string format);
        [DllImport("kernel32.dll")] private static extern UIntPtr GlobalSize(IntPtr memory);
        [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr memory);
        [DllImport("kernel32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalUnlock(IntPtr memory);
        [DllImport("shell32.dll",CharSet=CharSet.Unicode,EntryPoint="DragQueryFileW")] private static extern uint DragQueryFile(IntPtr drop,uint index,System.Text.StringBuilder path,uint size);
        [UnmanagedFunctionPointer(CallingConvention.Winapi,CharSet=CharSet.Unicode)]
        [return:MarshalAs(UnmanagedType.Bool)] private delegate bool OpenDialog(ref OpenFileName dialog);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int DialogError();
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true,EntryPoint="LoadLibraryW")] private static extern IntPtr LoadLibrary(string file);
        [DllImport("kernel32.dll",CharSet=CharSet.Ansi,ExactSpelling=true)] private static extern IntPtr GetProcAddress(IntPtr module,string name);
        [DllImport("kernel32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool FreeLibrary(IntPtr module);
    }
}
