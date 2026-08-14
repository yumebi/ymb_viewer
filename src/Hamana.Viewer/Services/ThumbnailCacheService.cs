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

    // 同一キャッシュファイルへの並行書き込み(コンバータと先読みの同時実行等)を直列化する。
    // 【v1.0.8】ファイルロックが使えない環境や並行スレッドからの同時書き込みによる
    // 破損・IO例外を防ぐため、staticロックで書込を直列化し、一時ファイル経由の
    // アトミックリネームで「部分書き込み済みファイル」が残らないようにする。
    private static readonly object WriteLock = new();

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
        // 並行スレッドからの同一ファイル書き込みを直列化する。
        lock (WriteLock)
        {
            try
            {
                Directory.CreateDirectory(CacheDir);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                // 一時ファイルへ書き込んでからリネームする(部分書き込みによる破損キャッシュを残さない)。
                var tmp = path + ".tmp";
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
                {
                    encoder.Save(fs);
                }
                File.Move(tmp, path, overwrite: true);
            }
            catch
            {
                try { if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp"); } catch { /* 無視 */ }
                // キャッシュ保存に失敗しても表示自体には影響しない
            }
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
