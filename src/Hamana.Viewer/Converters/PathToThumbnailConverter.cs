using System.Globalization;
using System.Windows.Data;
using Hamana.Viewer.Models;
using Hamana.Viewer.Services;

namespace Hamana.Viewer.Converters;

// サムネイル一覧用: ディスクキャッシュ経由で小さいデコード結果を返す(ImageEntryを丸ごと受け取る)。
public sealed class PathToThumbnailConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ImageEntry entry) return null;
        return ThumbnailCacheService.GetOrCreate(entry);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
