using System.IO;
using System.Windows.Media.Imaging;

namespace Hamana.Viewer.Services;

/// <summary>
/// デコード前に画像の寸法だけを読む。WPF標準(WIC)のデコーダでヘッダのみ解析し、
/// ピクセルは展開しない。読み取れない場合は例外を投げる(呼び出し側で握りつぶして null 扱いにする)。
/// </summary>
public static class ImageProbe
{
    public static (int Width, int Height)? TryGetSize(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ReadSize(stream);
    }

    public static (int Width, int Height)? TryGetSize(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        return ReadSize(stream);
    }

    private static (int Width, int Height)? ReadSize(Stream stream)
    {
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile,
            BitmapCacheOption.None);
        if (decoder.Frames.Count == 0) return null;

        var frame = decoder.Frames[0];
        return (frame.PixelWidth, frame.PixelHeight);
    }
}
