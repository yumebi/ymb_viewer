namespace Hamana.Viewer.Services;

/// <summary>
/// 外部(ファイル/アーカイブ)から読み込むデータのサイズ上限。
/// 巨大なエントリや画像によるメモリ枯渇(DoS)を防ぐための共通リミット。
/// </summary>
public static class SecurityLimits
{
    /// <summary>アーカイブ内から読み出す単一エントリの最大バイト数。</summary>
    public const long MaxEntryBytes = 256L * 1024 * 1024;

    /// <summary>1画像あたりの最大ピクセル数(8000x8000 程度)。</summary>
    public const long MaxPixels = 64_000_000;
}