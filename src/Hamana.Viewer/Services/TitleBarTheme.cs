using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Hamana.Viewer.Services;

/// <summary>
/// ウインドウのタイトルバーを、アプリのテーマ配色(YmbBgBrush / YmbFgBrush / YmbBorderBrush)に合わせる。
/// 何もしないと、ダークテーマでもタイトルバーだけ白い(システム設定の色)ままになる。
/// ダークモードの指定はWindows 10 2004以降、キャプション色などの指定はWindows 11以降で有効。
/// 非対応の環境では何も起きない。
/// </summary>
public static class TitleBarTheme
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void Apply(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        var bar = BrushColor(window, "YmbBgBrush");
        var text = BrushColor(window, "YmbFgBrush");
        var border = BrushColor(window, "YmbBorderBrush");
        if (bar is null) return;

        try
        {
            var dark = IsDark(bar.Value) ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

            var caption = ToColorRef(bar.Value);
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
            if (text is { } t)
            {
                var textRef = ToColorRef(t);
                DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref textRef, sizeof(int));
            }
            if (border is { } b)
            {
                var borderRef = ToColorRef(b);
                DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref borderRef, sizeof(int));
            }
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            // 古いWindows等で未対応。見た目が変わらないだけなので無視する。
        }
    }

    private static Color? BrushColor(Window window, string key) =>
        window.TryFindResource(key) is SolidColorBrush brush ? brush.Color : null;

    /// <summary>相対輝度が低い色なら暗い色とみなす。</summary>
    public static bool IsDark(Color c) => (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0 < 0.5;

    /// <summary>DWMが使うCOLORREF(0x00BBGGRR)に変換する。</summary>
    public static int ToColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);
}
