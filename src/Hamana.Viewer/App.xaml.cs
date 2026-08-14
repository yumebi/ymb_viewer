using System.Windows;
using System.Windows.Threading;

namespace Hamana.Viewer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 未処理例外でアプリが黙って落ちるのを防ぐグローバル安全網。
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            ShowError(args.Exception);
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            args.SetObserved();
            ShowError(args.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            ShowError(args.ExceptionObject as Exception);
        };
    }

    private static void ShowError(Exception? ex)
    {
        MessageBox.Show(
            "予期しないエラーが発生しました。\n\n" + ex?.Message,
            "Ymb Image Viewer",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
