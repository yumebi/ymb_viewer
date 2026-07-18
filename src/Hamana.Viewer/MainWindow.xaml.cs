using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Hamana.Viewer.Models;
using Hamana.Viewer.Services;
using Hamana.Viewer.ViewModels;
using Microsoft.Win32;

namespace Hamana.Viewer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    private Point? _dragStart;
    private double _dragStartH;
    private double _dragStartV;
    private bool _hasDragged;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        PopulateDriveRoots();

        var settings = AppSettingsService.Load();
        ApplyWindowSettings(settings);
        _viewModel.ApplySettingsBeforeLoad(settings);
        _viewModel.RestoreLastFolder(settings);

        Closing += MainWindow_Closing;
        Loaded += (_, _) => _ = _viewModel.CheckForUpdatesAsync();
    }

    private void ApplyWindowSettings(AppSettings settings)
    {
        if (settings.WindowWidth > 0) Width = settings.WindowWidth;
        if (settings.WindowHeight > 0) Height = settings.WindowHeight;

        if (settings.WindowLeft >= 0 && settings.WindowTop >= 0)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = settings.WindowLeft;
            Top = settings.WindowTop;
        }

        if (settings.WindowMaximized) WindowState = WindowState.Maximized;
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        var settings = _viewModel.CollectSettings();
        settings.WindowWidth = RestoreBounds.Width > 0 ? RestoreBounds.Width : Width;
        settings.WindowHeight = RestoreBounds.Height > 0 ? RestoreBounds.Height : Height;
        settings.WindowLeft = RestoreBounds.Left;
        settings.WindowTop = RestoreBounds.Top;
        settings.WindowMaximized = WindowState == WindowState.Maximized;
        AppSettingsService.Save(settings);
    }

    private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // サイドバー上でのホイールは一覧の内部スクロールに任せる(ページ送り/ズームにしない)。
        if (e.OriginalSource is DependencyObject source && IsDescendantOf(source, SidebarPanel))
        {
            return;
        }

        bool ctrl = Keyboard.Modifiers == ModifierKeys.Control;
        bool zoomMode = _viewModel.IsWheelZoomMode ^ ctrl;

        if (zoomMode)
        {
            ZoomAroundMouse(e);
        }
        else if (e.Delta < 0)
        {
            if (_viewModel.NextPageCommand.CanExecute(null))
                _viewModel.NextPageCommand.Execute(null);
        }
        else
        {
            if (_viewModel.PrevPageCommand.CanExecute(null))
                _viewModel.PrevPageCommand.Execute(null);
        }

        e.Handled = true;
    }

    private static bool IsDescendantOf(DependencyObject? element, DependencyObject ancestor)
    {
        while (element != null)
        {
            if (ReferenceEquals(element, ancestor)) return true;
            element = VisualTreeHelper.GetParent(element) ?? LogicalTreeHelper.GetParent(element);
        }

        return false;
    }

    // マウスカーソルの位置を中心にズームする(カーソル下の絵柄が動かないようスクロールオフセットを補正)。
    private void ZoomAroundMouse(MouseWheelEventArgs e)
    {
        double oldZoom = _viewModel.EffectiveZoom;
        var mousePos = e.GetPosition(ImageScrollViewer);
        double offsetXBefore = ImageScrollViewer.HorizontalOffset + mousePos.X;
        double offsetYBefore = ImageScrollViewer.VerticalOffset + mousePos.Y;

        _viewModel.ZoomBy(e.Delta > 0 ? 1.25 : 0.8);

        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            if (oldZoom <= 0) return;
            double ratio = _viewModel.EffectiveZoom / oldZoom;
            ImageScrollViewer.ScrollToHorizontalOffset(offsetXBefore * ratio - mousePos.X);
            ImageScrollViewer.ScrollToVerticalOffset(offsetYBefore * ratio - mousePos.Y);
        }));
    }

    private void ImageScrollViewer_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _viewModel.IsWheelZoomMode = !_viewModel.IsWheelZoomMode;
        e.Handled = true;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
        {
            OpenFolder();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _viewModel.IsFullScreen)
        {
            _viewModel.IsFullScreen = false;
            e.Handled = true;
        }
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths)
        {
            _viewModel.OpenPath(paths[0]);
        }
    }

    private void ThumbnailList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: ImageEntry entry })
        {
            _viewModel.GoToCommand.Execute(entry);
        }
    }

    private void FavoriteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: string path })
        {
            _viewModel.GoToFavoriteCommand.Execute(path);
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e) => OpenFolder();

    private void OpenFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "画像フォルダを選択"
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.LoadFolder(dialog.FolderName);
        }
    }

    private void OpenArchive_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "アーカイブを選択",
            Filter = "対応アーカイブ (*.zip;*.cbz;*.rar;*.cbr;*.7z;*.cb7)|*.zip;*.cbz;*.rar;*.cbr;*.7z;*.cb7|すべてのファイル (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.LoadArchive(dialog.FileName);
        }
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        new AboutWindow(_viewModel.AppVersion) { Owner = this }.ShowDialog();
    }

    private void UpdateBanner_Click(object sender, MouseButtonEventArgs e)
    {
        OpenUrl(_viewModel.UpdateUrl);
    }

    internal static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // ブラウザ起動に失敗しても致命的ではない
        }
    }

    // --- ズーム/回転: ビューポートに合わせたフィット倍率の再計算 ---

    // オフセット変化(パン操作)では再計算しない。ビューポート寸法が変わった時だけ反応する。
    private void ImageScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0)
        {
            RecalculateFit();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.PrimaryImage) or nameof(MainViewModel.SecondaryImage)
            or nameof(MainViewModel.IsSpreadMode) or nameof(MainViewModel.RotationAngle)
            or nameof(MainViewModel.IsAutoRotate) or nameof(MainViewModel.FitMode))
        {
            RecalculateFit();
        }
        else if (e.PropertyName == nameof(MainViewModel.IsFullScreen))
        {
            ApplyFullScreenState();
        }
    }

    private void ApplyFullScreenState()
    {
        if (_viewModel.IsFullScreen)
        {
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
        }
        else
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = WindowState.Normal;
        }
    }

    private void RecalculateFit()
    {
        var img = _viewModel.PrimaryImage;
        if (img is null)
        {
            _viewModel.FitScale = 1.0;
            return;
        }

        // 画像のMargin分だけ余裕を持たせ、Fitちょうどでスクロールバーが出て
        // ビューポート幅が変化→再計算→…と振動しないようにする。
        int pageCount = _viewModel.IsSpreadMode && _viewModel.SecondaryImage != null ? 2 : 1;
        double viewportW = Math.Max(0, ImageScrollViewer.ViewportWidth - 8) / pageCount;
        double viewportH = Math.Max(0, ImageScrollViewer.ViewportHeight - 8);

        double pixelW = img.PixelWidth;
        double pixelH = img.PixelHeight;
        FitMode mode = _viewModel.FitMode;

        // 単一ページ表示時のみ: 0度/90度のうちより大きく収まる方へ自動回転
        if (_viewModel.IsAutoRotate && pageCount == 1)
        {
            double fit0 = ComputeFit(pixelW, pixelH, viewportW, viewportH, mode);
            double fit90 = ComputeFit(pixelH, pixelW, viewportW, viewportH, mode);
            double targetAngle = fit90 > fit0 ? 90 : 0;

            if (_viewModel.RotationAngle != targetAngle)
            {
                _viewModel.SetAutoRotationAngle(targetAngle);
                return; // RotationAngle変更で再度RecalculateFitが呼ばれる
            }
        }

        double naturalW = pixelW;
        double naturalH = pixelH;
        if (_viewModel.RotationAngle is 90 or 270)
        {
            (naturalW, naturalH) = (naturalH, naturalW);
        }

        _viewModel.FitScale = ComputeFit(naturalW, naturalH, viewportW, viewportH, mode);
    }

    private static double ComputeFit(double naturalW, double naturalH, double viewportW, double viewportH, FitMode mode)
    {
        if (naturalW <= 0 || naturalH <= 0 || viewportW <= 0 || viewportH <= 0) return 1.0;
        double scaleX = viewportW / naturalW;
        double scaleY = viewportH / naturalH;

        return mode switch
        {
            FitMode.FitWidth => scaleX,
            FitMode.FitHeight => scaleY,
            FitMode.Cover => Math.Max(scaleX, scaleY),
            _ => Math.Min(scaleX, scaleY),
        };
    }

    // --- ズーム時のドラッグパン ---

    private const double ClickDragThreshold = 5.0;

    private void ImageScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(ImageScrollViewer);
        _dragStartH = ImageScrollViewer.HorizontalOffset;
        _dragStartV = ImageScrollViewer.VerticalOffset;
        _hasDragged = false;
        ImageScrollViewer.CaptureMouse();
    }

    private void ImageScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is null || e.LeftButton != MouseButtonState.Pressed) return;

        var pos = e.GetPosition(ImageScrollViewer);
        var delta = pos - _dragStart.Value;

        if (Math.Abs(delta.X) > ClickDragThreshold || Math.Abs(delta.Y) > ClickDragThreshold)
        {
            _hasDragged = true;
        }

        ImageScrollViewer.ScrollToHorizontalOffset(_dragStartH - delta.X);
        ImageScrollViewer.ScrollToVerticalOffset(_dragStartV - delta.Y);
    }

    private void ImageScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        bool wasClick = _dragStart is not null && !_hasDragged;
        var clickPos = _dragStart;

        _dragStart = null;
        ImageScrollViewer.ReleaseMouseCapture();

        if (wasClick && clickPos is not null && ImageScrollViewer.ActualWidth > 0)
        {
            // 画面左半分クリック=前のページ、右半分=次のページ(ドラッグと判定された場合は送らない)
            if (clickPos.Value.X < ImageScrollViewer.ActualWidth / 2)
            {
                if (_viewModel.PrevPageCommand.CanExecute(null)) _viewModel.PrevPageCommand.Execute(null);
            }
            else
            {
                if (_viewModel.NextPageCommand.CanExecute(null)) _viewModel.NextPageCommand.Execute(null);
            }
        }
    }

    // --- フォルダツリー(遅延ロード) ---

    private void PopulateDriveRoots()
    {
        FolderTree.Items.Clear();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady) continue;
            FolderTree.Items.Add(CreateTreeItem(drive.RootDirectory.FullName, drive.Name));
        }
    }

    private static TreeViewItem CreateTreeItem(string path, string header)
    {
        var item = new TreeViewItem { Header = header, Tag = path };
        item.Items.Add(new TreeViewItem { Header = "読み込み中...", Tag = null });
        item.Expanded += TreeViewItem_Expanded;
        return item;
    }

    private static void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
    {
        if (sender is not TreeViewItem item) return;
        if (item.Items.Count != 1 || (item.Items[0] as TreeViewItem)?.Tag is not null) return;

        item.Items.Clear();
        var path = (string)item.Tag;

        List<string> subDirs;
        try
        {
            subDirs = Directory.EnumerateDirectories(path)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return;
        }

        foreach (var dir in subDirs)
        {
            item.Items.Add(CreateTreeItem(dir, Path.GetFileName(dir)));
        }
    }

    private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeViewItem { Tag: string path } && Directory.Exists(path))
        {
            _viewModel.LoadFolder(path);
        }
    }
}
