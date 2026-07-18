namespace Hamana.Viewer.Models;

public sealed class ImageEntry
{
    public required string FullPath { get; init; }
    public required string FileName { get; init; }

    // nullなら通常ファイル。非nullならFullPathはアーカイブ本体のパスで、これがアーカイブ内エントリキー。
    public string? ArchiveEntryKey { get; init; }

    public bool IsArchiveEntry => ArchiveEntryKey is not null;

    // 先読み/サムネイルキャッシュ用の一意キー(アーカイブ内エントリはFullPathが重複するため)。
    public string CacheKey => ArchiveEntryKey is null ? FullPath : $"{FullPath}::{ArchiveEntryKey}";
}
