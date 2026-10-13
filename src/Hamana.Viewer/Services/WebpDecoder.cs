using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hamana.Viewer.Models;
using SkiaSharp;

namespace Hamana.Viewer.Services;

/// <summary>
/// WPFの標準BitmapDecoder(WIC)はWebPに非対応のため、SkiaSharpで
/// デコードして Pbgra32 の BitmapSource に変換する。
/// 静的WebP(VP8/VP8L)のみ対応。アニメーションWebPは先頭フレームを表示する。
/// 拡張子ではなくファイルの中身で形式を判定し、WebP以外はデコードしない。
/// </summary>
public static class WebpDecoder
{
    private const double DefaultDpi = 96.0;

    public static bool IsWebp(string fileName) =>
        string.Equals(Path.GetExtension(fileName), ".webp", StringComparison.OrdinalIgnoreCase);

    /// <summary>ファイルをデコードして BitmapSource を返す。失敗時は null。</summary>
    public static BitmapSource? DecodeFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return Decode(stream, resizeTo: null);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>バイト列(アーカイブ内の画像)をデコードする。失敗時は null。</summary>
    public static BitmapSource? DecodeBytes(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            return Decode(stream, resizeTo: null);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>サムネイル用: DecodePixelWidth相当まで縮小してデコードする。</summary>
    public static BitmapSource? DecodeThumbnail(ImageEntry entry, int maxDimension)
    {
        try
        {
            if (entry.ArchiveEntryKey is null)
            {
                using var stream = File.OpenRead(entry.FullPath);
                return Decode(stream, resizeTo: maxDimension);
            }

            var bytes = ArchiveImageService.ReadEntryBytes(entry.FullPath, entry.ArchiveEntryKey);
            using var ms = new MemoryStream(bytes, writable: false);
            return Decode(ms, resizeTo: maxDimension);
        }
        catch
        {
            return null;
        }
    }

    private static BitmapSource? Decode(Stream stream, int? resizeTo)
    {
        // Create は形式を判別できなければ null を返す。WebP以外はここで弾く。
        using var codec = SKCodec.Create(stream);
        if (codec is null || codec.EncodedFormat != SKEncodedImageFormat.Webp) return null;

        // ピクセルを展開する前に寸法を検査し、メモリDoSを防ぐ。
        var size = codec.Info;
        if (size.Width <= 0 || size.Height <= 0) return null;
        if (SecurityLimits.ExceedsPixelLimit(size.Width, size.Height)) return null;

        // WPFのPbgra32(乗算済みアルファ)に合わせる。縮小も乗算済みの状態で行うのが正しい。
        var info = new SKImageInfo(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        if (codec.GetPixels(info, bitmap.GetPixels()) != SKCodecResult.Success) return null;

        if (resizeTo is > 0 && (bitmap.Width > resizeTo || bitmap.Height > resizeTo))
        {
            double scale = Math.Min((double)resizeTo.Value / bitmap.Width, (double)resizeTo.Value / bitmap.Height);
            int width = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
            int height = Math.Max(1, (int)Math.Round(bitmap.Height * scale));

            var resizedInfo = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var resized = bitmap.Resize(resizedInfo, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            return resized is null ? null : ToBitmapSource(resized);
        }

        return ToBitmapSource(bitmap);
    }

    private static BitmapSource ToBitmapSource(SKBitmap bitmap)
    {
        int stride = bitmap.RowBytes;
        byte[] pixels = new byte[stride * bitmap.Height];
        Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);

        var source = BitmapSource.Create(
            bitmap.Width, bitmap.Height, DefaultDpi, DefaultDpi, PixelFormats.Pbgra32, null, pixels, stride);
        source.Freeze();
        return source;
    }
}
