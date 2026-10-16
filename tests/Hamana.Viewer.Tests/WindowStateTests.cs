using System.Windows.Media;
using Hamana.Viewer.Models;
using Hamana.Viewer.Services;
using Xunit;

namespace Hamana.Viewer.Tests;

public class WindowStateTests
{
    private static AppSettings Settings(double left, double top, double width = 1000, double height = 700) =>
        new() { WindowLeft = left, WindowTop = top, WindowWidth = width, WindowHeight = height };

    [Fact]
    public void PixelValues_ArePreferred()
    {
        var s = Settings(10, 10);
        s.WindowPxX = 300;
        s.WindowPxY = 200;
        s.WindowPxWidth = 1100;
        s.WindowPxHeight = 700;

        Assert.Equal(new PxRect(300, 200, 1100, 700), WindowStateHelper.ToDesiredBounds(s, 1.5));
    }

    [Fact]
    public void LegacyDipValues_AreConvertedWithTheSystemScale()
    {
        var r = WindowStateHelper.ToDesiredBounds(Settings(100, 50, 800, 600), 1.5);
        Assert.Equal(new PxRect(150, 75, 1200, 900), r);
    }

    [Fact]
    public void LegacyUnsetMarker_MeansNoSavedPosition()
    {
        // 以前は「未設定」を (-1,-1) で表していた。
        Assert.Null(WindowStateHelper.ToDesiredBounds(Settings(-1, -1), 1.0));
    }

    [Theory]
    [InlineData(-1280, 200)]    // プライマリの左にあるモニタ
    [InlineData(300, -900)]     // プライマリの上にあるモニタ
    [InlineData(-2000, -500)]
    public void NegativeCoordinates_AreValidPositions(double left, double top)
    {
        // 以前は 0 未満をすべて「未設定」とみなしていて、左や上のモニタの位置が復元されなかった。
        var r = WindowStateHelper.ToDesiredBounds(Settings(left, top), 1.0);
        Assert.NotNull(r);
        Assert.Equal((int)left, r!.Value.X);
        Assert.Equal((int)top, r.Value.Y);
    }

    [Fact]
    public void ZeroSize_MeansNoSavedBounds()
    {
        Assert.Null(WindowStateHelper.ToDesiredBounds(Settings(10, 10, 0, 0), 1.0));
    }

    [Theory]
    [InlineData("#1E1E1E", true)]
    [InlineData("#2D2D30", true)]
    [InlineData("#F5F6F8", false)]
    public void IsDark_FollowsBrightness(string hex, bool expected)
    {
        Assert.Equal(expected, TitleBarTheme.IsDark((Color)ColorConverter.ConvertFromString(hex)));
    }

    [Fact]
    public void ToColorRef_IsBlueGreenRedOrder()
    {
        Assert.Equal(0x332211, TitleBarTheme.ToColorRef(Color.FromRgb(0x11, 0x22, 0x33)));
    }
}
