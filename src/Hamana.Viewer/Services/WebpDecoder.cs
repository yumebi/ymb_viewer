using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hamana.Viewer.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using RgbaImage = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;

namespace Hamana.Viewer.Services;

/// <summary>
/// WPFの標準BitmapDecoder(WIC)はWebPに非対応のため、SixLabors.ImageSharpで
/// デコードして Bgra32 の BitmapSource に変換する。
/// 静的WebP(VP8/VP8L)のみ対応。アニメーションWebPは先頭フレームを表示する。
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
            using var image = Image.Load<Rgba32>(path);
            return ToBitmapSource(image, resizeTo: null);
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
            using var image = Image.Load<Rgba32>(bytes);
            return ToBitmapSource(image, resizeTo: null);
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
            using var image = entry.ArchiveEntryKey is null
                ? Image.Load<Rgba32>(entry.FullPath)
                : Image.Load<Rgba32>(ArchiveImageService.ReadEntryBytes(entry.FullPath, entry.ArchiveEntryKey));
            return ToBitmapSource(image, resizeTo: maxDimension);
        }
        catch
        {
            return null;
        }
    }

    private static BitmapSource ToBitmapSource(RgbaImage image, int? resizeTo)
    {
        if (resizeTo is > 0 && (image.Width > resizeTo || image.Height > resizeTo))
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(resizeTo.Value, resizeTo.Value),
            }));
        }

        int width = image.Width;
        int height = image.Height;
        int stride = width * 4;

        // ImageSharpはRGBA順でコピーされるため、WPFのBgra32に合わせてR/Bを入れ替える。
        byte[] rgba = new byte[height * stride];
        image.CopyPixelDataTo(rgba);
        byte[] bgra = new byte[height * stride];
        for (int i = 0; i < rgba.Length; i += 4)
        {
            bgra[i] = rgba[i + 2];       // B
            bgra[i + 1] = rgba[i + 1];   // G
            bgra[i + 2] = rgba[i];       // R
            bgra[i + 3] = rgba[i + 3];   // A
        }

        var bitmap = BitmapSource.Create(
            width, height, DefaultDpi, DefaultDpi, PixelFormats.Bgra32, null, bgra, stride);
        bitmap.Freeze();
        return bitmap;
    }
}
