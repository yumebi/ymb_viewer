using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Hamana.Viewer.Services;
using Xunit;

namespace Hamana.Viewer.Tests;

/// <summary>
/// TestData/animated.webp は 64x48 の4フレーム(ロスレス):
///   0: 全面が赤 / 1: 赤の上に青い四角(8,8)-(23,23) / 2: 全面が緑 / 3: 緑の上に黄色い四角(40,24)-(55,39)
///   長さは 100 / 200 / 5 / 300 ms(5ms は短すぎるので 100ms に直される)。
/// 1 と 3 は前のフレームの上に差分を重ねる形式なので、前提フレームの扱いが正しくないと色が崩れる。
/// </summary>
public class AnimatedWebpTests
{
    private static byte[] Fixture() =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestData", "animated.webp"));

    // B, G, R, A の順
    private static byte[] Pixel(BitmapSource source, int x, int y)
    {
        var px = new byte[4];
        source.CopyPixels(new Int32Rect(x, y, 1, 1), px, 4, 0);
        return px;
    }

    private static void AssertColor(BitmapSource source, int x, int y, byte r, byte g, byte b)
    {
        var px = Pixel(source, x, y);
        Assert.Equal(new byte[] { b, g, r, 255 }, px);
    }

    [Fact]
    public void DecodesAllFrames_WithSizeAndDelays()
    {
        var frames = AnimatedWebpService.DecodeFrames(Fixture());

        Assert.NotNull(frames);
        Assert.Equal(4, frames!.Count);
        Assert.All(frames, f => Assert.Equal((64, 48), (f.Image.PixelWidth, f.Image.PixelHeight)));
        Assert.Equal(TimeSpan.FromMilliseconds(100), frames[0].Delay);
        Assert.Equal(TimeSpan.FromMilliseconds(200), frames[1].Delay);
        Assert.Equal(TimeSpan.FromMilliseconds(100), frames[2].Delay);   // 5ms → 既定の100ms
        Assert.Equal(TimeSpan.FromMilliseconds(300), frames[3].Delay);
    }

    [Fact]
    public void Frame0_IsSolidRed()
    {
        var f = AnimatedWebpService.DecodeFrames(Fixture())![0].Image;
        AssertColor(f, 0, 0, 255, 0, 0);
        AssertColor(f, 63, 47, 255, 0, 0);
        AssertColor(f, 15, 15, 255, 0, 0);
    }

    [Fact]
    public void Frame1_BlueSquareOverRed_DependsOnFrame0()
    {
        var f = AnimatedWebpService.DecodeFrames(Fixture())![1].Image;
        AssertColor(f, 15, 15, 0, 0, 255);   // 四角の中は青
        AssertColor(f, 0, 0, 255, 0, 0);     // 四角の外は、前のフレームの赤が残っている
        AssertColor(f, 40, 40, 255, 0, 0);
    }

    [Fact]
    public void Frame2_IsSolidGreen_NoLeftoverFromPreviousFrames()
    {
        var f = AnimatedWebpService.DecodeFrames(Fixture())![2].Image;
        AssertColor(f, 15, 15, 0, 200, 0);   // 前のフレームの青い四角が残っていない
        AssertColor(f, 0, 0, 0, 200, 0);
    }

    [Fact]
    public void Frame3_YellowSquareOverGreen()
    {
        var f = AnimatedWebpService.DecodeFrames(Fixture())![3].Image;
        AssertColor(f, 47, 31, 255, 255, 0);
        AssertColor(f, 0, 0, 0, 200, 0);
        AssertColor(f, 15, 15, 0, 200, 0);
    }

    [Fact]
    public void Frames_AreFrozen_SoTheyCanBeUsedFromAnyThread()
    {
        var frames = AnimatedWebpService.DecodeFrames(Fixture())!;
        Assert.All(frames, f => Assert.True(f.Image.IsFrozen));
    }

    [Fact]
    public void NonWebp_ReturnsNull()
    {
        Assert.Null(AnimatedWebpService.DecodeFrames(new byte[] { 1, 2, 3, 4, 5 }));
        Assert.Null(AnimatedWebpService.DecodeFrames(Array.Empty<byte>()));
    }

    [Fact]
    public void StaticWebp_ReturnsNull_SoTheNormalPathIsUsed()
    {
        // 静止画のWebP(1フレーム)はアニメーションとして扱わない
        using var bmp = new SkiaSharp.SKBitmap(8, 8);
        bmp.Erase(SkiaSharp.SKColors.Red);
        using var img = SkiaSharp.SKImage.FromBitmap(bmp);
        using var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Webp, 100);
        Assert.Null(AnimatedWebpService.DecodeFrames(data.ToArray()));
    }

    [Fact]
    public void TruncatedAnimation_DoesNotThrow()
    {
        var bytes = Fixture();
        var cut = bytes.Take(bytes.Length / 2).ToArray();
        // 壊れたファイルでアプリが落ちない(null か、デコードできた分のどちらか)
        var ex = Record.Exception(() => AnimatedWebpService.DecodeFrames(cut));
        Assert.Null(ex);
    }
}
