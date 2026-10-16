using System.Net.Http;

namespace Hamana.Viewer.Services;

public sealed record UpdateCheckResult(bool IsNewer, string? LatestVersion, string ReleaseUrl);

// GitHub の最新リリース(Releases API)と現行バージョンを比較する。
// version.json ではなく公開済みのリリースを見るのは、リリースに失敗しても version.json だけ
// 先に更新されて、存在しない版を案内してしまうのを避けるため。
// オフライン時・API制限時は例外を握りつぶし「更新なし」を返す。
public static class UpdateCheckService
{
    private const string Repo = "yumebi/ymb_viewer";
    private const string ReleasesUrl = "https://github.com/" + Repo + "/releases/latest";

    public static async Task<UpdateCheckResult> CheckAsync(Version currentVersion)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("YmbImageViewer-UpdateCheck");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            var json = await client.GetStringAsync(GitHubRelease.ApiUrl(Repo));
            var release = GitHubRelease.Parse(json, Repo);
            if (release is null)
            {
                return new UpdateCheckResult(false, null, ReleasesUrl);
            }

            var isNewer = GitHubRelease.IsNewer(release.Version, currentVersion.ToString());
            return new UpdateCheckResult(isNewer, release.Version, release.Url ?? ReleasesUrl);
        }
        catch
        {
            return new UpdateCheckResult(false, null, ReleasesUrl);
        }
    }
}
