using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Tinycast.Platform;

internal static class ShellIcons
{
    const int JumboPx = 256;
    const int ExtraLargePx = 48;
    const string CacheVersion = "v7";

    public static string? FromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) && !Directory.Exists(path))
            return null;
        var dest = CachePath("file-" + CacheVersion + ":" + path.ToLowerInvariant());
        if (IsFresh(dest))
            return dest;

        if (FromShellItem(path, dest, out var shellSide) && shellSide >= 64)
            return dest;

        var index = SysIconIndex(path);
        var bestSide = 0;
        foreach (var size in new[] { NativeMethods.ShilJumbo, NativeMethods.ShilExtraLarge })
        {
            var icon = IconFromImageList(size, index);
            if (icon == IntPtr.Zero)
                icon = DefExtract(path, size == NativeMethods.ShilJumbo ? JumboPx : ExtraLargePx);
            if (icon == IntPtr.Zero)
                continue;
            try
            {
                var px = Math.Max(IconExtent(icon), ExtraLargePx);
                if (size == NativeMethods.ShilJumbo && px < ExtraLargePx)
                    continue;
                if (!RasterizeIcon(icon, px, out var bgra, out var width) || bgra is null)
                    continue;
                var cropped = CropToContent(bgra, width, width, out var side);
                if (cropped is null || side <= bestSide)
                    continue;
                if (!WritePng(cropped, side, dest))
                    continue;
                bestSide = side;
            }
            finally
            {
                NativeMethods.DestroyIcon(icon);
            }
        }

        if (bestSide > 0)
            return dest;
        return FromLegacyLarge(path, dest);
    }

    public static string? FromAppUserModelId(string aumid)
    {
        if (string.IsNullOrWhiteSpace(aumid))
            return null;
        var dest = CachePath("aumid-" + CacheVersion + ":" + aumid.ToLowerInvariant());
        if (IsFresh(dest))
            return dest;
        return FromShellItem(@"shell:AppsFolder\" + aumid, dest, out var side) && side >= 24
            ? dest
            : null;
    }

    public static string? FromLogo(IRandomAccessStreamReference? logo, string key)
    {
        if (logo is null || string.IsNullOrWhiteSpace(key))
            return null;
        var dest = CachePath("logo-" + CacheVersion + ":" + key);
        if (IsFresh(dest))
            return dest;
        try
        {
            using var ras = logo.OpenReadAsync().AsTask().GetAwaiter().GetResult();
            if (ras.Size == 0)
                return null;
            var decoder = BitmapDecoder.CreateAsync(ras).AsTask().GetAwaiter().GetResult();
            var pixels = decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Straight,
                    new BitmapTransform(),
                    ExifOrientationMode.IgnoreExifOrientation,
                    ColorManagementMode.DoNotColorManage)
                .AsTask().GetAwaiter().GetResult()
                .DetachPixelData();
            if (SaveCropped(pixels, (int)decoder.PixelWidth, (int)decoder.PixelHeight, dest, out _))
                return dest;
        }
        catch (Exception ex)
        {
            Log.Write("logo " + key + ": " + ex.Message);
        }

        return null;
    }

    static bool FromShellItem(string path, string dest, out int side)
    {
        side = 0;
        IShellItemImageFactory? factory = null;
        try
        {
            var iid = NativeMethods.IidIShellItemImageFactory;
            if (NativeMethods.SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out factory) != 0
                || factory is null)
                return false;
            var size = new NativeMethods.SizeXy { Cx = JumboPx, Cy = JumboPx };
            var flags = NativeMethods.SiigbfBiggerSizeOk
                | NativeMethods.SiigbfIconOnly
                | NativeMethods.SiigbfCropToSquare
                | NativeMethods.SiigbfScaleUp;
            if (factory.GetImage(size, flags, out var hbm) != 0 || hbm == IntPtr.Zero)
                return false;
            try
            {
                if (!BgraFromHbitmap(hbm, out var bgra, out var width, out var height) || bgra is null)
                    return false;
                var cropped = CropToContent(bgra, width, height, out side);
                if (cropped is null || side < ExtraLargePx / 2)
                    return false;
                return WritePng(cropped, side, dest);
            }
            finally
            {
                NativeMethods.DeleteObject(hbm);
            }
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            if (factory is not null)
                Marshal.ReleaseComObject(factory);
        }
    }

    static string? FromLegacyLarge(string path, string dest)
    {
        var info = new NativeMethods.ShFileInfo();
        var result = NativeMethods.SHGetFileInfo(
            path, 0, ref info, (uint)Marshal.SizeOf<NativeMethods.ShFileInfo>(),
            NativeMethods.ShgfiIcon | NativeMethods.ShgfiLargeIcon);
        if (result == IntPtr.Zero || info.HIcon == IntPtr.Zero)
            return null;
        try
        {
            if (!RasterizeIcon(info.HIcon, 32, out var bgra, out var width) || bgra is null)
                return null;
            return SaveCropped(bgra, width, width, dest, out _) ? dest : null;
        }
        finally
        {
            NativeMethods.DestroyIcon(info.HIcon);
        }
    }

    static int SysIconIndex(string path)
    {
        var info = new NativeMethods.ShFileInfo();
        NativeMethods.SHGetFileInfo(
            path, 0, ref info, (uint)Marshal.SizeOf<NativeMethods.ShFileInfo>(),
            NativeMethods.ShgfiSysIconIndex);
        return info.IIcon;
    }

    static IntPtr IconFromImageList(int list, int index)
    {
        if (index < 0)
            return IntPtr.Zero;
        try
        {
            var iid = NativeMethods.IidIImageList;
            if (NativeMethods.SHGetImageList(list, ref iid, out var images) != 0 || images is null)
                return IntPtr.Zero;
            try
            {
                if (images.GetIcon(index, NativeMethods.IldTransparent, out var icon) != 0)
                    return IntPtr.Zero;
                return icon;
            }
            finally
            {
                Marshal.ReleaseComObject(images);
            }
        }
        catch (Exception)
        {
            return IntPtr.Zero;
        }
    }

    static IntPtr DefExtract(string path, int px)
    {
        try
        {
            if (NativeMethods.SHDefExtractIcon(path, 0, 0, out var icon, IntPtr.Zero, (uint)px) != 0)
                return IntPtr.Zero;
            return icon;
        }
        catch (Exception)
        {
            return IntPtr.Zero;
        }
    }

    static int IconExtent(IntPtr hIcon)
    {
        if (!NativeMethods.GetIconInfo(hIcon, out var info))
            return ExtraLargePx;
        try
        {
            var handle = info.HbmColor != IntPtr.Zero ? info.HbmColor : info.HbmMask;
            if (handle == IntPtr.Zero || NativeMethods.GetObject(handle, Marshal.SizeOf<NativeMethods.GdiBitmap>(), out var bmp) == 0)
                return ExtraLargePx;
            return Math.Max(bmp.BmWidth, Math.Abs(bmp.BmHeight));
        }
        finally
        {
            if (info.HbmColor != IntPtr.Zero)
                NativeMethods.DeleteObject(info.HbmColor);
            if (info.HbmMask != IntPtr.Zero)
                NativeMethods.DeleteObject(info.HbmMask);
        }
    }

    static bool RasterizeIcon(IntPtr hIcon, int px, out byte[]? bgra, out int width)
    {
        bgra = null;
        width = px = Math.Clamp(px, ExtraLargePx, JumboPx);
        var hdc = NativeMethods.CreateCompatibleDC(IntPtr.Zero);
        if (hdc == IntPtr.Zero)
            return false;
        var bmi = new NativeMethods.BitmapInfo
        {
            BmiHeader = new NativeMethods.BitmapInfoHeader
            {
                BiSize = Marshal.SizeOf<NativeMethods.BitmapInfoHeader>(),
                BiWidth = px,
                BiHeight = -px,
                BiPlanes = 1,
                BiBitCount = 32,
                BiCompression = NativeMethods.BiRgb,
            },
        };
        var dib = NativeMethods.CreateDIBSection(hdc, ref bmi, 0, out var bits, IntPtr.Zero, 0);
        if (dib == IntPtr.Zero || bits == IntPtr.Zero)
        {
            NativeMethods.DeleteDC(hdc);
            return false;
        }

        var old = NativeMethods.SelectObject(hdc, dib);
        NativeMethods.DrawIconEx(hdc, 0, 0, hIcon, px, px, 0, IntPtr.Zero, NativeMethods.DiNormal);
        bgra = new byte[px * px * 4];
        Marshal.Copy(bits, bgra, 0, bgra.Length);
        NativeMethods.SelectObject(hdc, old);
        NativeMethods.DeleteObject(dib);
        NativeMethods.DeleteDC(hdc);
        return true;
    }

    static bool BgraFromHbitmap(IntPtr hbm, out byte[]? bgra, out int width, out int height)
    {
        bgra = null;
        width = 0;
        height = 0;
        if (NativeMethods.GetObject(hbm, Marshal.SizeOf<NativeMethods.GdiBitmap>(), out var bmp) == 0)
            return false;
        width = bmp.BmWidth;
        height = Math.Abs(bmp.BmHeight);
        if (width <= 0 || height <= 0)
            return false;
        var hdc = NativeMethods.CreateCompatibleDC(IntPtr.Zero);
        if (hdc == IntPtr.Zero)
            return false;
        var bmi = new NativeMethods.BitmapInfo
        {
            BmiHeader = new NativeMethods.BitmapInfoHeader
            {
                BiSize = Marshal.SizeOf<NativeMethods.BitmapInfoHeader>(),
                BiWidth = width,
                BiHeight = -height,
                BiPlanes = 1,
                BiBitCount = 32,
                BiCompression = NativeMethods.BiRgb,
            },
        };
        bgra = new byte[width * height * 4];
        var rows = NativeMethods.GetDIBits(hdc, hbm, 0, (uint)height, bgra, ref bmi, 0);
        NativeMethods.DeleteDC(hdc);
        return rows > 0;
    }

    static bool SaveCropped(byte[] bgra, int width, int height, string dest, out int side)
    {
        var cropped = CropToContent(bgra, width, height, out side);
        if (cropped is null || side < 8)
            return false;
        return WritePng(cropped, side, dest);
    }

    static byte[]? CropToContent(byte[] bgra, int width, int height, out int side)
    {
        side = 0;
        var bounds = OpaqueRect(bgra, width, height);
        if (bounds.Count == 0)
            return null;

        var contentW = bounds.MaxX - bounds.MinX + 1;
        var contentH = bounds.MaxY - bounds.MinY + 1;
        var outer = Math.Max(contentW, contentH);
        if (outer < 8)
            return null;

        var pad = Math.Clamp(outer / 24, 2, 12);
        side = Math.Min(Math.Max(width, height), outer + pad * 2);
        var cx = (bounds.MinX + bounds.MaxX + 1) / 2;
        var cy = (bounds.MinY + bounds.MaxY + 1) / 2;
        var x0 = Math.Clamp(cx - side / 2, 0, Math.Max(0, width - side));
        var y0 = Math.Clamp(cy - side / 2, 0, Math.Max(0, height - side));
        if (x0 + side > width)
            side = width - x0;
        if (y0 + side > height)
            side = Math.Min(side, height - y0);

        if (side < 8)
            return null;

        var cropped = new byte[side * side * 4];
        for (var y = 0; y < side; y++)
        {
            var srcY = y0 + y;
            if (srcY < 0 || srcY >= height)
                continue;
            var src = (srcY * width + x0) * 4;
            var dst = y * side * 4;
            var bytes = Math.Min(side, width - x0) * 4;
            if (bytes > 0)
                System.Buffer.BlockCopy(bgra, src, cropped, dst, bytes);
        }

        return cropped;
    }

    static (int MinX, int MinY, int MaxX, int MaxY, int Count) OpaqueRect(byte[] bgra, int width, int height)
    {
        var useRgb = !HasAlpha(bgra);
        var minX = width;
        var minY = height;
        var maxX = 0;
        var maxY = 0;
        var count = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (!IsInk(bgra, (y * width + x) * 4, useRgb))
                    continue;
                count++;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        return count == 0 ? (0, 0, 0, 0, 0) : (minX, minY, maxX, maxY, count);
    }

    static bool HasAlpha(byte[] bgra)
    {
        for (var i = 3; i < bgra.Length; i += 4)
        {
            if (bgra[i] != 0)
                return true;
        }

        return false;
    }

    static bool IsInk(byte[] bgra, int i, bool useRgb)
    {
        if (useRgb)
            return bgra[i] > 18 || bgra[i + 1] > 18 || bgra[i + 2] > 18;
        return bgra[i + 3] >= 80;
    }

    static bool WritePng(byte[] bgra, int side, string dest)
    {
        try
        {
            using var stream = new InMemoryRandomAccessStream();
            var encoder = BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream).AsTask().GetAwaiter().GetResult();
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight,
                (uint)side, (uint)side, 96, 96, bgra);
            encoder.FlushAsync().AsTask().GetAwaiter().GetResult();
            stream.Seek(0);
            var reader = new DataReader(stream.GetInputStreamAt(0));
            try
            {
                var size = (uint)stream.Size;
                reader.LoadAsync(size).AsTask().GetAwaiter().GetResult();
                var png = new byte[size];
                reader.ReadBytes(png);
                File.WriteAllBytes(dest, png);
                return true;
            }
            finally
            {
                reader.Dispose();
            }
        }
        catch (Exception ex)
        {
            Log.Write("icon png: " + ex.Message);
            return false;
        }
    }

    static string CachePath(string key)
    {
        Directory.CreateDirectory(AppPaths.IconCacheDir);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..20];
        return Path.Combine(AppPaths.IconCacheDir, hash + ".png");
    }

    static bool IsFresh(string dest) =>
        File.Exists(dest) && new FileInfo(dest).Length > 0;
}
