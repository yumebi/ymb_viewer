using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hamana.Viewer.Models;
using SkiaSharp;

namespace Hamana.Viewer.Services;

/// <summary>
/// アニメーションWebPの各フレームとフレーム間隔を取り出し、WPFのBitmapSourceに変換する。
/// GIF用の <see cref="GifFrame"/> と同じ形で返すので、再生は GIF と同じ仕組み(MainViewModel)で行える。
///
/// WebPのアニメーションは「前のフレームの上に差分を重ねる」形式で、フレームによっては
/// 画面全体ではなく一部の矩形だけを持つ。SkiaSharp にはどのフレームが前提になるか
/// (RequiredFrame)を教えてもらえるので、その前提フレームの画素を作業用バッファに戻してから
/// 次のフレームをデコードする。
/// </summary>
public static class AnimatedWebpService
{
    private const int MaxFrames = 256;

    /// <summary>全フレームの画素の合計の上限。巨大なアニメーションによるメモリ枯渇を防ぐ。</summary>
    private const long MaxTotalBytes = 512L * 1024 * 1024;

    private const double DefaultDpi = 96.0;
    private const int DefaultDelayMs = 100;

    /// <summary>
    /// ブラウザ等と同じく、極端に短い(10ms以下)指定は既定の間隔に直す。
    /// そのままだと CPU を使い切るほど速く再生してしまう。
    /// </summary>
    private const int MinDelayMs = 10;

    public static List<GifFrame>? TryLoadFrames(ImageEntry entry)
    {
        if (!WebpDecoder.IsWebp(entry.FileName))
            return null;

        try
        {
            var bytes = entry.ArchiveEntryKey is null
                ? File.ReadAllBytes(entry.FullPath)
                : ArchiveImageService.ReadEntryBytes(entry.FullPath, entry.ArchiveEntryKey);
            return DecodeFrames(bytes);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// アニメーションWebPのバイト列から全フレームを取り出す。
    /// 静止画のWebP、WebP以外、サイズ・枚数が上限を超えるもの、デコードに失敗したものは null。
    /// </summary>
    public static List<GifFrame>? DecodeFrames(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var codec = SKCodec.Create(stream);
        if (codec is null || codec.EncodedFormat != SKEncodedImageFormat.Webp)
            return null;

        var size = codec.Info;
        if (size.Width <= 0 || size.Height <= 0 || SecurityLimits.ExceedsPixelLimit(size.Width, size.Height))
            return null;

        var frameCount = codec.FrameCount;
        if (frameCount <= 1 || frameCount > MaxFrames)
            return null;

        long frameBytes = (long)size.Width * size.Height * 4;
        if (frameBytes * frameCount > MaxTotalBytes)
            return null;

        var frameInfos = codec.FrameInfo;
        var info = new SKImageInfo(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        int stride = info.RowBytes;

        // 各フレームの完成した画素(画面全体)。差分フレームの前提として再利用する。
        var pixelsByFrame = new byte[frameCount][];
        var frames = new List<GifFrame>(frameCount);

        for (int i = 0; i < frameCount; i++)
        {
            var work = new byte[frameBytes];
            int required = frameInfos[i].RequiredFrame;
            var options = new SKCodecOptions(i);
            if (required >= 0)
            {
                // 前提フレームの画素を土台にして、その上に今回の差分を重ねてもらう
                Buffer.BlockCopy(pixelsByFrame[required], 0, work, 0, work.Length);
                options = new SKCodecOptions(i, required);
            }

            // 管理配列をデコード先として固定する(unsafe を使わずに済ませる)
            var handle = GCHandle.Alloc(work, GCHandleType.Pinned);
            try
            {
                if (codec.GetPixels(info, handle.AddrOfPinnedObject(), stride, options) != SKCodecResult.Success)
                    return null;
            }
            finally
            {
                handle.Free();
            }

            pixelsByFrame[i] = work;

            var source = BitmapSource.Create(
                size.Width, size.Height, DefaultDpi, DefaultDpi, PixelFormats.Pbgra32, null, work, stride);
            source.Freeze();

            int delayMs = frameInfos[i].Duration;
            if (delayMs <= MinDelayMs)
                delayMs = DefaultDelayMs;
            frames.Add(new GifFrame(source, TimeSpan.FromMilliseconds(delayMs)));
        }

        return frames;
    }
}
