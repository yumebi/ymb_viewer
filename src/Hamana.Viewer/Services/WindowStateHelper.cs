using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Hamana.Viewer.Models;

namespace Hamana.Viewer.Services;

/// <summary>
/// ウインドウの位置・サイズを、物理ピクセルのまま保存・復元するための補助。
/// WPFのLeft/TopはDIP(論理ピクセル)で、DPIの異なるモニタが混在すると換算がずれるため、
/// Win32のGetWindowPlacement / SetWindowPosを使う。復元時は現在のモニタ構成に合わせて補正する
/// (WindowPlacementMath.Fit)。
/// </summary>
public static class WindowStateHelper
{
    private const int SW_SHOWMINIMIZED = 2;
    private const int SW_SHOWMAXIMIZED = 3;
    private const int WPF_RESTORETOMAXIMIZED = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public POINT ptMinPosition;
        public POINT ptMaxPosition;
        public RECT rcNormalPosition;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    /// <summary>ウインドウのDPI倍率(96dpi=1.0)。取得できなければシステムDPI。</summary>
    public static double Scale(IntPtr hwnd)
    {
        var dpi = hwnd == IntPtr.Zero ? 0 : GetDpiForWindow(hwnd);
        if (dpi == 0) dpi = GetDpiForSystem();
        return dpi == 0 ? 1.0 : dpi / 96.0;
    }

    /// <summary>
    /// 保存された設定から、復元したい物理ピクセルの矩形を求める。位置が保存されていなければ null。
    /// 新形式(ピクセル)を優先し、無ければ旧形式(DIP)を、システムDPIの倍率で換算して使う。
    /// 旧形式は「未設定」を -1 で表していたため、(-1,-1) だけを未設定とみなす
    /// (以前は 0 未満をすべて未設定にしていて、左や上のモニタの位置が復元されなかった)。
    /// </summary>
    public static PxRect? ToDesiredBounds(AppSettings settings, double systemScale)
    {
        if (settings.WindowPxX is { } px && settings.WindowPxY is { } py
            && settings.WindowPxWidth is > 0 && settings.WindowPxHeight is > 0)
        {
            return new PxRect(px, py, settings.WindowPxWidth.Value, settings.WindowPxHeight.Value);
        }

        var legacyUnset = settings.WindowLeft == -1 && settings.WindowTop == -1;
        if (legacyUnset || settings.WindowWidth <= 0 || settings.WindowHeight <= 0) return null;

        return new PxRect(
            (int)Math.Round(settings.WindowLeft * systemScale), (int)Math.Round(settings.WindowTop * systemScale),
            (int)Math.Round(settings.WindowWidth * systemScale), (int)Math.Round(settings.WindowHeight * systemScale));
    }

    /// <summary>
    /// 位置とサイズを復元する。HWNDが作られた後・初回表示の前(SourceInitialized)に呼ぶので、
    /// 既定の位置に一瞬出てから動く、ということが起きない。
    /// </summary>
    public static void ApplyBounds(Window window, PxRect desired)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        var scale = Scale(hwnd);
        var fitted = WindowPlacementMath.Fit(
            desired, MonitorWorkAreas.Enumerate(), (int)(window.MinWidth * scale), (int)(window.MinHeight * scale));

        // 別DPIのモニタへ動かすと、WM_DPICHANGEDで大きさが換算し直されることがある。
        // 2回目で保存したピクセル値そのものに揃える。
        for (var i = 0; i < 2; i++)
        {
            SetWindowPos(hwnd, IntPtr.Zero, fitted.X, fitted.Y, fitted.Width, fitted.Height, SWP_NOZORDER | SWP_NOACTIVATE);
        }
    }

    /// <summary>
    /// 現在の「通常の状態」の位置とサイズ(物理ピクセル、スクリーン座標)と、最大化の状態を取得する。
    /// 最大化・最小化の最中でも、通常の状態の位置が取れる。
    /// </summary>
    public static (PxRect Normal, bool Maximized)? Capture(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return null;

        var placement = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
        if (!GetWindowPlacement(hwnd, ref placement)) return null;

        // rcNormalPositionは「プライマリの作業領域の左上を原点とする座標」なので、スクリーン座標に直す。
        var primary = MonitorWorkAreas.Enumerate().FirstOrDefault();
        var n = placement.rcNormalPosition;
        var rect = new PxRect(n.Left + primary.X, n.Top + primary.Y, n.Right - n.Left, n.Bottom - n.Top);
        if (rect.Width <= 0 || rect.Height <= 0) return null;

        // 最大化していたウインドウを最小化した場合も「最大化」として扱う。
        var maximized = placement.showCmd == SW_SHOWMAXIMIZED
            || (placement.showCmd == SW_SHOWMINIMIZED && (placement.flags & WPF_RESTORETOMAXIMIZED) != 0);
        return (rect, maximized);
    }
}
