using System.IO;
using Hamana.Viewer.Models;

namespace Hamana.Viewer.Services;

public static class FolderImageService
{
    public static readonly string[] SupportedExtensions =
    [
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tif", ".tiff"
    ];

    public static List<ImageEntry> LoadFolder(string folderPath, SortMode sortMode, bool descending)
    {
        var naturalComparer = new NaturalStringComparer();

        var files = Directory.EnumerateFiles(folderPath)
            .Where(p => SupportedExtensions.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase));

        IOrderedEnumerable<string> ordered = sortMode switch
        {
            SortMode.DateModified => descending
                ? files.OrderByDescending(File.GetLastWriteTimeUtc)
                : files.OrderBy(File.GetLastWriteTimeUtc),
            SortMode.Size => descending
                ? files.OrderByDescending(p => new FileInfo(p).Length)
                : files.OrderBy(p => new FileInfo(p).Length),
            _ => descending
                ? files.OrderByDescending(Path.GetFileName, naturalComparer)
                : files.OrderBy(Path.GetFileName, naturalComparer),
        };

        return ordered
            .Select(p => new ImageEntry { FullPath = p, FileName = Path.GetFileName(p) })
            .ToList();
    }

    public static bool IsSupportedImage(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
