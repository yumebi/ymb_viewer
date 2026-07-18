using System.Net.Http;
using System.Text.Json;

namespace Hamana.Viewer.Services;

public sealed record UpdateCheckResult(bool IsNewer, string? LatestVersion, string ReleaseUrl);

// version.json ( https://github.com/yumebi/ymb_viewer ) と現行バージョンを比較する。
// リポジトリ未公開時やオフライン時は例外を握りつぶし「更新なし」を返す。
public static class UpdateCheckService
{
    private const string VersionJsonUrl = "https://raw.githubusercontent.com/yumebi/ymb_viewer/master/version.json";
    private const string ReleasesUrl = "https://github.com/yumebi/ymb_viewer/releases/latest";

    public static async Task<UpdateCheckResult> CheckAsync(Version currentVersion)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("YmbImageViewer-UpdateCheck");

            var json = await client.GetStringAsync(VersionJsonUrl);
            using var doc = JsonDocument.Parse(json);
            var versionText = doc.RootElement.GetProperty("version").GetString();

            if (versionText is null || !Version.TryParse(NormalizeVersion(versionText), out var latest))
            {
                return new UpdateCheckResult(false, versionText, ReleasesUrl);
            }

            return new UpdateCheckResult(latest > currentVersion, versionText, ReleasesUrl);
        }
        catch
        {
            return new UpdateCheckResult(false, null, ReleasesUrl);
        }
    }

    private static string NormalizeVersion(string text) => text.TrimStart('v', 'V');
}
