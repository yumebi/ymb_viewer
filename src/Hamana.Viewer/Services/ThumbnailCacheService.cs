using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;
using Hamana.Viewer.Models;

namespace Hamana.Viewer.Services;

// サムネイルを %LocalAppData% にディスクキャッシュする。
// フォルダを開き直すたび/アーカイブをスクロールするたびの再デコードを避ける。
public static class ThumbnailCacheService
{
    private const int DecodePixelWidth = 120;

    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YmbImageViewer", "thumbcache");

    public static BitmapImage? GetOrCreate(ImageEntry entry)
    {
        try
        {
            string cachePath = Path.Combine(CacheDir, BuildCacheKey(entry) + ".png");

            if (File.Exists(cachePath))
            {
                var cached = TryLoadFromDisk(cachePath);
                if (cached is not null) return cached;
            }

            var decoded = DecodeSource(entry);
            if (decoded is null) return null;

            SaveAsPng(decoded, cachePath);
            return decoded;
        }
        catch
        {
            return null;
        }
    }

    private static string BuildCacheKey(ImageEntry entry)
    {
        string identity;
        try
        {
            var info = new FileInfo(entry.FullPath);
            identity = entry.ArchiveEntryKey is null
                ? $"{entry.FullPath}|{info.LastWriteTimeUtc.Ticks}|{info.Length}"
                : $"{entry.FullPath}|{entry.ArchiveEntryKey}|{info.LastWriteTimeUtc.Ticks}";
        }
        catch
        {
            identity = entry.CacheKey;
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return Convert.ToHexString(hash);
    }

    private static BitmapImage? DecodeSource(ImageEntry entry)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = DecodePixelWidth;

            if (entry.ArchiveEntryKey is null)
            {
                bitmap.UriSource = new Uri(entry.FullPath, UriKind.Absolute);
                bitmap.EndInit();
            }
            else
            {
                var bytes = ArchiveImageService.ReadEntryBytes(entry.FullPath, entry.ArchiveEntryKey);
                using var ms = new MemoryStream(bytes);
                bitmap.StreamSource = ms;
                bitmap.EndInit();
            }

            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static void SaveAsPng(BitmapImage image, string path)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            encoder.Save(fs);
        }
        catch
        {
            // キャッシュ保存に失敗しても表示自体には影響しない
        }
    }

    private static BitmapImage? TryLoadFromDisk(string path)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}
