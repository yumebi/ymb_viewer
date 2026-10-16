using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hamana.Viewer.Models;

namespace Hamana.Viewer.Services;

public static class AppSettingsService
{
    /// <summary>開発・動作確認用。設定すると、設定ファイルの保存先をこのフォルダに切り替える。</summary>
    public const string DataDirEnvVar = "YMB_HAMANA_DATA_DIR";

    // 本番の設定(最後に開いたフォルダなど)に触れずに起動して確かめられるよう、環境変数で保存先を変えられる。
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetEnvironmentVariable(DataDirEnvVar) is { Length: > 0 } overrideDir
            ? overrideDir
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YmbImageViewer"),
        "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
        }
        catch
        {
            // 破損/読み取り不可時は既定値で起動する
        }

        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // 保存失敗は致命的ではないので無視する
        }
    }
}
