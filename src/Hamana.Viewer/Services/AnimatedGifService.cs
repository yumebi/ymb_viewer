using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Hamana.Viewer.Models;
using GdiBitmap = System.Drawing.Bitmap;
using GdiFrameDimension = System.Drawing.Imaging.FrameDimension;

namespace Hamana.Viewer.Services;

public sealed record GifFrame(BitmapSource Image, TimeSpan Delay);

// GIFの各フレームとフレーム間隔を取り出し、WPFのBitmapSourceに変換する。
public static class AnimatedGifService
{
    private const int PropertyTagFrameDelay = 0x5100;

    public static List<GifFrame>? TryLoadFrames(ImageEntry entry)
    {
        if (!string.Equals(Path.GetExtension(entry.FileName), ".gif", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            using GdiBitmap bitmap = entry.ArchiveEntryKey is null
                ? new GdiBitmap(entry.FullPath)
                : new GdiBitmap(new MemoryStream(ArchiveImageService.ReadEntryBytes(entry.FullPath, entry.ArchiveEntryKey)));

            var dimension = new GdiFrameDimension(bitmap.FrameDimensionsList[0]);
            int frameCount = bitmap.GetFrameCount(dimension);
            if (frameCount <= 1) return null;

            byte[]? delayBytes = null;
            if (Array.IndexOf(bitmap.PropertyIdList, PropertyTagFrameDelay) >= 0)
            {
                delayBytes = bitmap.GetPropertyItem(PropertyTagFrameDelay)?.Value;
            }

            var frames = new List<GifFrame>(frameCount);

            for (int i = 0; i < frameCount; i++)
            {
                bitmap.SelectActiveFrame(dimension, i);

                int delayMs = 100;
                if (delayBytes is not null && delayBytes.Length >= (i + 1) * 4)
                {
                    delayMs = BitConverter.ToInt32(delayBytes, i * 4) * 10;
                    if (delayMs <= 0) delayMs = 100;
                }

                using var frameBitmap = new GdiBitmap(bitmap);
                var hBitmap = frameBitmap.GetHbitmap();
                try
                {
                    var src = Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    src.Freeze();
                    frames.Add(new GifFrame(src, TimeSpan.FromMilliseconds(delayMs)));
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }

            return frames;
        }
        catch
        {
            return null;
        }
    }

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}
