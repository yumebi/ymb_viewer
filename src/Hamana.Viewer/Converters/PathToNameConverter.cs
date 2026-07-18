using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace Hamana.Viewer.Converters;

// フルパスからフォルダ名(末尾セグメント)だけを表示するための変換。
public sealed class PathToNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || path.Length == 0) return value ?? string.Empty;

        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrEmpty(name) ? path : name;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
