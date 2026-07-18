using System.IO;
using Hamana.Viewer.Models;
using SharpCompress.Archives;

namespace Hamana.Viewer.Services;

// zip/cbz/rar/cbr/7z/cb7 内の画像を、解凍せずに一覧・読み出しする。
public static class ArchiveImageService
{
    public static readonly string[] SupportedExtensions =
    [
        ".zip", ".cbz", ".rar", ".cbr", ".7z", ".cb7"
    ];

    public static bool IsSupportedArchive(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static List<ImageEntry> ListImages(string archivePath, SortMode sortMode, bool descending)
    {
        using var archive = ArchiveFactory.Open(archivePath);
        var comparer = new NaturalStringComparer();

        var entries = archive.Entries
            .Where(e => !e.IsDirectory && e.Key is not null &&
                        FolderImageService.SupportedExtensions.Contains(Path.GetExtension(e.Key), StringComparer.OrdinalIgnoreCase))
            .ToList();

        IEnumerable<IArchiveEntry> ordered = sortMode switch
        {
            SortMode.DateModified => descending
                ? entries.OrderByDescending(e => e.LastModifiedTime ?? DateTime.MinValue)
                : entries.OrderBy(e => e.LastModifiedTime ?? DateTime.MinValue),
            SortMode.Size => descending
                ? entries.OrderByDescending(e => e.Size)
                : entries.OrderBy(e => e.Size),
            _ => descending
                ? entries.OrderByDescending(e => e.Key, comparer)
                : entries.OrderBy(e => e.Key, comparer),
        };

        return ordered
            .Select(e => new ImageEntry
            {
                FullPath = archivePath,
                FileName = Path.GetFileName(e.Key!),
                ArchiveEntryKey = e.Key
            })
            .ToList();
    }

    public static byte[] ReadEntryBytes(string archivePath, string entryKey)
    {
        using var archive = ArchiveFactory.Open(archivePath);
        var entry = archive.Entries.First(e => e.Key == entryKey);

        using var ms = new MemoryStream();
        using (var stream = entry.OpenEntryStream())
        {
            stream.CopyTo(ms);
        }

        return ms.ToArray();
    }
}
