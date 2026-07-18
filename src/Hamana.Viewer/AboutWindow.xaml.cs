using System.Windows;
using System.Windows.Input;

namespace Hamana.Viewer;

public partial class AboutWindow : Window
{
    public AboutWindow(string version)
    {
        InitializeComponent();
        VersionText.Text = $"v{version}";
    }

    private void RepoLink_Click(object sender, MouseButtonEventArgs e) =>
        MainWindow.OpenUrl("https://github.com/yumebi/ymb_viewer");

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
