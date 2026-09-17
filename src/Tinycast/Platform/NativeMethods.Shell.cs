using System.Runtime.InteropServices;

namespace Tinycast.Platform;

internal static partial class NativeMethods
{
    public const uint ShgfiIcon = 0x000000100;
    public const uint ShgfiLargeIcon = 0x000000000;
    public const uint ShgfiSysIconIndex = 0x00004000;
    public const uint DiNormal = 0x0003;
    public const int IldTransparent = 1;
    public const int ShilExtraLarge = 2;
    public const int ShilJumbo = 4;
    public const int SiigbfBiggerSizeOk = 0x1;
    public const int SiigbfIconOnly = 0x4;
    public const int SiigbfCropToSquare = 0x20;
    public const int SiigbfScaleUp = 0x100;
    public const uint SeeMaskNoAsync = 0x00000100;
    public const uint SeeMaskFlagNoUi = 0x00000400;
    public const int SwShownormal = 1;
    public const int BiRgb = 0;

    [DllImport("dwmapi.dll")]
    public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins pMarInset);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int w, int h);

    [DllImport("user32.dll")]
    public static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr ho);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    public static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateDIBSection(
        IntPtr hdc, ref BitmapInfo pbmi, uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);

    [DllImport("user32.dll")]
    public static extern bool DrawIconEx(
        IntPtr hdc, int xLeft, int yTop, IntPtr hIcon, int cxWidth, int cyWidth,
        uint istepIfAniCur, IntPtr hbrFlickerFreeDraw, uint diFlags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SHGetFileInfo(
        string pszPath, uint dwFileAttributes, ref ShFileInfo psfi, uint cbFileInfo, uint uFlags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool ShellExecuteEx(ref ShellExecuteInfo lpExecInfo);

    [DllImport("shell32.dll", EntryPoint = "#727")]
    public static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);

    public static bool TryGetImageList(int iImageList, out IImageList? list)
    {
        list = null;
        var iid = IidIImageList;
        var hr = SHGetImageList(iImageList, ref iid, out var images);
        if (hr < 0 || images is null)
            return false;
        list = images;
        return true;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int SHDefExtractIcon(
        string pszIconFile, int iIndex, uint uFlags, out IntPtr phiconLarge, IntPtr phiconSmall, uint nIconSize);

    [DllImport("user32.dll")]
    public static extern bool GetIconInfo(IntPtr hIcon, out IconInfo piconinfo);

    [DllImport("gdi32.dll")]
    public static extern int GetObject(IntPtr hgdiobj, int cbBuffer, out GdiBitmap lpvObject);

    [DllImport("gdi32.dll")]
    public static extern int GetDIBits(
        IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines,
        byte[] lpvBits, ref BitmapInfo lpbi, uint uUsage);

    [DllImport("shell32.dll")]
    public static extern int SHGetKnownFolderPath(in Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int SHCreateItemFromParsingName(
        string pszPath, IntPtr pbc, ref Guid riid, out IShellItemImageFactory ppv);

    [StructLayout(LayoutKind.Sequential)]
    public struct SizeXy
    {
        public int Cx;
        public int Cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Margins
    {
        public int CxLeftWidth;
        public int CxRightWidth;
        public int CyTopHeight;
        public int CyBottomHeight;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct ShFileInfo
    {
        public IntPtr HIcon;
        public int IIcon;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string TypeName;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BitmapInfoHeader
    {
        public int BiSize;
        public int BiWidth;
        public int BiHeight;
        public ushort BiPlanes;
        public ushort BiBitCount;
        public int BiCompression;
        public uint BiSizeImage;
        public int BiXPelsPerMeter;
        public int BiYPelsPerMeter;
        public uint BiClrUsed;
        public uint BiClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BitmapInfo
    {
        public BitmapInfoHeader BmiHeader;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IconInfo
    {
        public bool FIcon;
        public int XHotspot;
        public int YHotspot;
        public IntPtr HbmMask;
        public IntPtr HbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GdiBitmap
    {
        public int BmType;
        public int BmWidth;
        public int BmHeight;
        public int BmWidthBytes;
        public ushort BmPlanes;
        public ushort BmBitsPixel;
        public IntPtr BmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ImageListDrawParams
    {
        public int CbSize;
        public IntPtr Himl;
        public int I;
        public IntPtr HdcDst;
        public int X;
        public int Y;
        public int Cx;
        public int Cy;
        public int XBitmap;
        public int YBitmap;
        public int RgbBk;
        public int RgbFg;
        public int FStyle;
        public int DwRop;
        public int FState;
        public int Frame;
        public int CrEffect;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct ShellExecuteInfo
    {
        public int CbSize;
        public uint FMask;
        public IntPtr Hwnd;
        public string? LpVerb;
        public string LpFile;
        public string? LpParameters;
        public string? LpDirectory;
        public int NShow;
        public IntPtr HInstApp;
        public IntPtr LpIDList;
        public string? LpClass;
        public IntPtr HkeyClass;
        public uint DwHotKey;
        public IntPtr HIcon;
        public IntPtr HProcess;
    }

    public static Guid IidIImageList = new("46EB5926-582E-4017-9FDF-E8998DAA0950");
    public static Guid IidIShellItemImageFactory = new("bcc18b79-ba16-442f-80c4-8a59c30c463b");
}

[ComImport]
[Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IImageList
{
    [PreserveSig] int Add(IntPtr hbmImage, IntPtr hbmMask, out int pi);
    [PreserveSig] int ReplaceIcon(int i, IntPtr hicon, out int pi);
    [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
    [PreserveSig] int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
    [PreserveSig] int AddMasked(IntPtr hbmImage, int crMask, out int pi);
    [PreserveSig] int Draw(ref NativeMethods.ImageListDrawParams pimldp);
    [PreserveSig] int Remove(int i);
    [PreserveSig] int GetIcon(int i, int flags, out IntPtr picon);
}

[ComImport]
[Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IShellItemImageFactory
{
    [PreserveSig]
    int GetImage(NativeMethods.SizeXy size, int flags, out IntPtr phbm);
}
