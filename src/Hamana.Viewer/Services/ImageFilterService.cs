using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Hamana.Viewer.Services;

// 明るさ/コントラスト/彩度/シャープネスを掛けたビットマップを作る。
// Task.Run内での使用を想定した純粋なCPU処理(WriteableBitmapはFreeze後スレッド間で共有可)。
public static class ImageFilterService
{
    public static BitmapSource Apply(BitmapSource source, double brightness, double contrast, double saturation, double sharpness)
    {
        if (brightness == 0 && contrast == 0 && saturation == 0 && sharpness == 0)
        {
            return source;
        }

        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int width = converted.PixelWidth;
        int height = converted.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[height * stride];
        converted.CopyPixels(pixels, stride, 0);

        double b = brightness / 100.0 * 255.0;
        double c = 1.0 + contrast / 100.0;
        double s = 1.0 + saturation / 100.0;

        for (int i = 0; i < pixels.Length; i += 4)
        {
            double bl = pixels[i];
            double gr = pixels[i + 1];
            double rd = pixels[i + 2];

            rd = (rd - 128) * c + 128 + b;
            gr = (gr - 128) * c + 128 + b;
            bl = (bl - 128) * c + 128 + b;

            double gray = rd * 0.299 + gr * 0.587 + bl * 0.114;
            rd = gray + (rd - gray) * s;
            gr = gray + (gr - gray) * s;
            bl = gray + (bl - gray) * s;

            pixels[i] = (byte)Clamp(bl);
            pixels[i + 1] = (byte)Clamp(gr);
            pixels[i + 2] = (byte)Clamp(rd);
        }

        if (sharpness > 0)
        {
            pixels = Sharpen(pixels, width, height, stride, sharpness / 100.0);
        }

        var result = new WriteableBitmap(width, height, source.DpiX, source.DpiY, PixelFormats.Bgra32, null);
        result.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
        result.Freeze();
        return result;
    }

    private static double Clamp(double v) => v < 0 ? 0 : v > 255 ? 255 : v;

    // 簡易アンシャープマスク(ラプラシアン4近傍)
    private static byte[] Sharpen(byte[] src, int width, int height, int stride, double amount)
    {
        byte[] dst = (byte[])src.Clone();

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                int idx = y * stride + x * 4;
                for (int ch = 0; ch < 3; ch++)
                {
                    int center = src[idx + ch];
                    int up = src[idx - stride + ch];
                    int down = src[idx + stride + ch];
                    int left = src[idx - 4 + ch];
                    int right = src[idx + 4 + ch];
                    double laplacian = center * 4 - up - down - left - right;
                    dst[idx + ch] = (byte)Clamp(center + laplacian * amount);
                }
            }
        }

        return dst;
    }
}
