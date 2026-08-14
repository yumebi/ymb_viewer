using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media.Imaging;
using Hamana.Viewer.Models;

namespace Hamana.Viewer.Services;

// カレントページ周辺を非同期に先読みしてキャッシュしておくことで、
// ページ送り時の表示遅延を無くすためのキャッシュ。
public sealed class ImagePreloadCache
{
    private readonly ConcurrentDictionary<string, Task<BitmapSource?>> _cache = new();

    public Task<BitmapSource?> GetAsync(ImageEntry entry)
    {
        return _cache.GetOrAdd(entry.CacheKey, _ => LoadAsync(entry));
    }

    public void PreloadAround(IReadOnlyList<ImageEntry> entries, int centerIndex, int radius = 2)
    {
        if (entries.Count == 0) return;

        int lo = Math.Max(0, centerIndex - radius);
        int hi = Math.Min(entries.Count - 1, centerIndex + radius);
        var keep = new HashSet<string>();

        for (int i = lo; i <= hi; i++)
        {
            var entry = entries[i];
            keep.Add(entry.CacheKey);
            _ = GetAsync(entry);
        }

        foreach (var key in _cache.Keys)
        {
            if (!keep.Contains(key))
            {
                _cache.TryRemove(key, out _);
            }
        }
    }

    private static Task<BitmapSource?> LoadAsync(ImageEntry entry)
    {
        return Task.Run(() =>
        {
            try
            {
                // WPFのWICはWebPに非対応のため、WebPはImageSharp経由でデコードする。
                if (WebpDecoder.IsWebp(entry.FileName))
                    return entry.ArchiveEntryKey is null
                        ? WebpDecoder.DecodeFile(entry.FullPath)
                        : WebpDecoder.DecodeBytes(ArchiveImageService.ReadEntryBytes(entry.FullPath, entry.ArchiveEntryKey));

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;

                if (entry.ArchiveEntryKey is null)
                {
                    bitmap.UriSource = new Uri(entry.FullPath, UriKind.Absolute);
                }
                else
                {
                    var bytes = ArchiveImageService.ReadEntryBytes(entry.FullPath, entry.ArchiveEntryKey);
                    using var ms = new MemoryStream(bytes);
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return (BitmapSource?)bitmap;
                }

                bitmap.EndInit();
                bitmap.Freeze();
                return (BitmapSource?)bitmap;
            }
            catch
            {
                return null;
            }
        });
    }
}
