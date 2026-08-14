using System.Collections.ObjectModel;

namespace Hamana.Viewer.Models;

/// <summary>内容が同一(SHA-256一致)の画像のグループ。重複検出の結果表示に使う。</summary>
public sealed class DuplicateGroup
{
    public DuplicateGroup(IReadOnlyList<ImageEntry> entries)
    {
        foreach (var e in entries)
            Entries.Add(e);
    }

    public ObservableCollection<ImageEntry> Entries { get; } = new();

    public string Title => $"重複 {Entries.Count} 枚";

    public override string ToString() => Title;
}
