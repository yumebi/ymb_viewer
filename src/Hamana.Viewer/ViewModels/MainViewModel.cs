using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Hamana.Viewer.Models;
using Hamana.Viewer.Services;

namespace Hamana.Viewer.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private const double MinZoom = 0.1;
    private const double MaxZoom = 8.0;

    private readonly ImagePreloadCache _cache = new();
    private readonly DispatcherTimer _slideshowTimer;

    private int _currentIndex = -1;
    private bool _isSpreadMode;
    private bool _isSidebarVisible;
    private bool _isRightToLeft = true;
    private bool _isFullScreen;
    private BitmapImage? _primaryImage;
    private BitmapImage? _secondaryImage;
    private string _statusText = "フォルダを開いてください (Ctrl+O)";
    private string? _folderPath;

    private double _rotationAngle;
    private double _zoomMultiplier = 1.0;
    private double _fitScale = 1.0;

    private bool _isSlideshowActive;
    private double _slideshowIntervalSeconds = 3.0;

    private SortMode _sortMode = SortMode.NameNatural;
    private bool _sortDescending;

    private bool _isAutoRotate;
    private bool _isWheelZoomMode;
    private BoundaryAction _boundaryAction = BoundaryAction.Loop;
    private FitMode _fitMode = FitMode.Contain;

    private double _brightness;
    private double _contrast;
    private double _saturation;
    private double _sharpness;
    private bool _isFilterPanelVisible;
    private BitmapSource? _renderedPrimaryImage;
    private BitmapSource? _renderedSecondaryImage;
    private readonly DispatcherTimer _filterDebounceTimer;

    private bool _isUpdateAvailable;
    private string? _latestVersion;
    private string _updateUrl = "https://github.com/yumebi/ymb_viewer/releases/latest";

    public ObservableCollection<ImageEntry> Entries { get; } = new();
    public ObservableCollection<string> FavoriteDirectories { get; } = new();

    public string? CurrentFolderPath => _folderPath;

    public string AppVersion { get; } =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    public int CurrentIndex
    {
        get => _currentIndex;
        private set => SetField(ref _currentIndex, value);
    }

    public bool IsSpreadMode
    {
        get => _isSpreadMode;
        set
        {
            if (SetField(ref _isSpreadMode, value))
            {
                _ = UpdateCurrentImagesAsync();
            }
        }
    }

    public bool IsSidebarVisible
    {
        get => _isSidebarVisible;
        set => SetField(ref _isSidebarVisible, value);
    }

    public bool IsFullScreen
    {
        get => _isFullScreen;
        set => SetField(ref _isFullScreen, value);
    }

    public bool IsRightToLeft
    {
        get => _isRightToLeft;
        set => SetField(ref _isRightToLeft, value);
    }

    public BitmapImage? PrimaryImage
    {
        get => _primaryImage;
        private set => SetField(ref _primaryImage, value);
    }

    public BitmapImage? SecondaryImage
    {
        get => _secondaryImage;
        private set => SetField(ref _secondaryImage, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    // 現在ページの回転角(0/90/180/270)。ページ送りでリセットされる。
    public double RotationAngle
    {
        get => _rotationAngle;
        private set => SetField(ref _rotationAngle, value);
    }

    // ユーザー操作によるズーム倍率。1.0 = Fit(等倍調整なし)。
    public double ZoomMultiplier
    {
        get => _zoomMultiplier;
        private set
        {
            if (SetField(ref _zoomMultiplier, value))
            {
                OnPropertyChanged(nameof(EffectiveZoom));
            }
        }
    }

    // ビューポートに収まるよう code-behind が計算するスケール。
    public double FitScale
    {
        get => _fitScale;
        set
        {
            if (SetField(ref _fitScale, value))
            {
                OnPropertyChanged(nameof(EffectiveZoom));
            }
        }
    }

    public double EffectiveZoom => FitScale * ZoomMultiplier;

    public bool IsSlideshowActive
    {
        get => _isSlideshowActive;
        set
        {
            if (!SetField(ref _isSlideshowActive, value)) return;

            if (value)
            {
                _slideshowTimer.Interval = TimeSpan.FromSeconds(Math.Max(0.5, SlideshowIntervalSeconds));
                _slideshowTimer.Start();
            }
            else
            {
                _slideshowTimer.Stop();
            }
        }
    }

    public double SlideshowIntervalSeconds
    {
        get => _slideshowIntervalSeconds;
        set
        {
            double clamped = Math.Max(0.5, value);
            if (!SetField(ref _slideshowIntervalSeconds, clamped)) return;

            if (IsSlideshowActive)
            {
                _slideshowTimer.Interval = TimeSpan.FromSeconds(clamped);
            }
        }
    }

    public SortMode SortMode
    {
        get => _sortMode;
        set
        {
            if (SetField(ref _sortMode, value)) Resort();
        }
    }

    public bool SortDescending
    {
        get => _sortDescending;
        set
        {
            if (SetField(ref _sortDescending, value)) Resort();
        }
    }

    // ON時: 0度/90度のうちビューポートに大きく収まる方へ自動回転(見開き時は対象外)。
    public bool IsAutoRotate
    {
        get => _isAutoRotate;
        set => SetField(ref _isAutoRotate, value);
    }

    // ON時: ホイール単独=ズーム / Ctrl+ホイール=ページ送り(OFF時はその逆)。右クリックで切替。
    public bool IsWheelZoomMode
    {
        get => _isWheelZoomMode;
        set => SetField(ref _isWheelZoomMode, value);
    }

    // 先頭/末尾到達時の挙動。Loop=先頭⇔末尾を周回、FolderNavigation=前後のフォルダへ移動。
    public BoundaryAction BoundaryAction
    {
        get => _boundaryAction;
        set => SetField(ref _boundaryAction, value);
    }

    // 画像の収め方。Contain=全体表示 / FitWidth=横幅優先 / FitHeight=縦幅優先 / Cover=画面全体を覆う。
    public FitMode FitMode
    {
        get => _fitMode;
        set => SetField(ref _fitMode, value);
    }

    // -100〜100。0=無効。
    public double Brightness
    {
        get => _brightness;
        set { if (SetField(ref _brightness, value)) ScheduleFilterRefresh(); }
    }

    public double Contrast
    {
        get => _contrast;
        set { if (SetField(ref _contrast, value)) ScheduleFilterRefresh(); }
    }

    public double Saturation
    {
        get => _saturation;
        set { if (SetField(ref _saturation, value)) ScheduleFilterRefresh(); }
    }

    // 0〜100。0=無効。
    public double Sharpness
    {
        get => _sharpness;
        set { if (SetField(ref _sharpness, value)) ScheduleFilterRefresh(); }
    }

    public bool IsFilterPanelVisible
    {
        get => _isFilterPanelVisible;
        set => SetField(ref _isFilterPanelVisible, value);
    }

    // フィルター適用後、実際にImageコントロールが表示する画像。
    public BitmapSource? RenderedPrimaryImage
    {
        get => _renderedPrimaryImage;
        private set => SetField(ref _renderedPrimaryImage, value);
    }

    public BitmapSource? RenderedSecondaryImage
    {
        get => _renderedSecondaryImage;
        private set => SetField(ref _renderedSecondaryImage, value);
    }

    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        private set => SetField(ref _isUpdateAvailable, value);
    }

    public string? LatestVersion
    {
        get => _latestVersion;
        private set => SetField(ref _latestVersion, value);
    }

    public string UpdateUrl
    {
        get => _updateUrl;
        private set => SetField(ref _updateUrl, value);
    }

    public RelayCommand NextPageCommand { get; }
    public RelayCommand PrevPageCommand { get; }
    public RelayCommand ToggleSpreadCommand { get; }
    public RelayCommand ToggleSidebarCommand { get; }
    public RelayCommand ToggleDirectionCommand { get; }
    public RelayCommand GoToCommand { get; }
    public RelayCommand RotateLeftCommand { get; }
    public RelayCommand RotateRightCommand { get; }
    public RelayCommand ZoomInCommand { get; }
    public RelayCommand ZoomOutCommand { get; }
    public RelayCommand ResetZoomCommand { get; }
    public RelayCommand ToggleSlideshowCommand { get; }
    public RelayCommand ToggleFullScreenCommand { get; }
    public RelayCommand ResetFiltersCommand { get; }
    public RelayCommand AddFavoriteCommand { get; }
    public RelayCommand RemoveFavoriteCommand { get; }
    public RelayCommand GoToFavoriteCommand { get; }

    public MainViewModel()
    {
        _slideshowTimer = new DispatcherTimer();
        _slideshowTimer.Tick += (_, _) => AdvanceSlideshow();

        _filterDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _filterDebounceTimer.Tick += (_, _) =>
        {
            _filterDebounceTimer.Stop();
            _ = RefreshRenderedImagesAsync();
        };

        NextPageCommand = new RelayCommand(_ => GoNext(), _ => Entries.Count > 0);
        PrevPageCommand = new RelayCommand(_ => GoPrev(), _ => Entries.Count > 0);
        ToggleSpreadCommand = new RelayCommand(_ => IsSpreadMode = !IsSpreadMode);
        ToggleSidebarCommand = new RelayCommand(_ => IsSidebarVisible = !IsSidebarVisible);
        ToggleDirectionCommand = new RelayCommand(_ => IsRightToLeft = !IsRightToLeft);
        GoToCommand = new RelayCommand(p =>
        {
            if (p is ImageEntry entry)
            {
                int index = Entries.IndexOf(entry);
                if (index >= 0) SetIndex(index);
            }
        });

        RotateLeftCommand = new RelayCommand(_ => RotationAngle = NormalizeAngle(RotationAngle - 90));
        RotateRightCommand = new RelayCommand(_ => RotationAngle = NormalizeAngle(RotationAngle + 90));
        ZoomInCommand = new RelayCommand(_ => ZoomMultiplier = Math.Clamp(ZoomMultiplier * 1.25, MinZoom, MaxZoom));
        ZoomOutCommand = new RelayCommand(_ => ZoomMultiplier = Math.Clamp(ZoomMultiplier / 1.25, MinZoom, MaxZoom));
        ResetZoomCommand = new RelayCommand(_ => ZoomMultiplier = 1.0);
        ToggleSlideshowCommand = new RelayCommand(_ => IsSlideshowActive = !IsSlideshowActive);
        ToggleFullScreenCommand = new RelayCommand(_ => IsFullScreen = !IsFullScreen);

        ResetFiltersCommand = new RelayCommand(_ =>
        {
            Brightness = 0;
            Contrast = 0;
            Saturation = 0;
            Sharpness = 0;
        });

        AddFavoriteCommand = new RelayCommand(
            _ =>
            {
                if (_folderPath is not null && !FavoriteDirectories.Contains(_folderPath, StringComparer.OrdinalIgnoreCase))
                {
                    FavoriteDirectories.Add(_folderPath);
                }
            },
            _ => _folderPath is not null);

        RemoveFavoriteCommand = new RelayCommand(p =>
        {
            if (p is string path) FavoriteDirectories.Remove(path);
        });

        GoToFavoriteCommand = new RelayCommand(p =>
        {
            if (p is string path && Directory.Exists(path)) LoadFolder(path);
        });
    }

    public void LoadFolder(string folderPath)
    {
        var entries = FolderImageService.LoadFolder(folderPath, SortMode, SortDescending);
        Entries.Clear();
        foreach (var e in entries) Entries.Add(e);

        _folderPath = folderPath;
        SetIndex(Entries.Count > 0 ? 0 : -1);
    }

    public void OpenPath(string path)
    {
        if (Directory.Exists(path))
        {
            LoadFolder(path);
        }
        else if (File.Exists(path) && FolderImageService.IsSupportedImage(path))
        {
            var folder = Path.GetDirectoryName(path)!;
            LoadFolder(folder);
            int index = Entries.ToList().FindIndex(e => string.Equals(e.FullPath, path, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) SetIndex(index);
        }
    }

    private void Resort()
    {
        if (_folderPath is null) return;

        string? currentPath = CurrentIndex >= 0 && CurrentIndex < Entries.Count ? Entries[CurrentIndex].FullPath : null;

        var entries = FolderImageService.LoadFolder(_folderPath, SortMode, SortDescending);
        Entries.Clear();
        foreach (var e in entries) Entries.Add(e);

        int newIndex = currentPath is null
            ? -1
            : Entries.ToList().FindIndex(e => string.Equals(e.FullPath, currentPath, StringComparison.OrdinalIgnoreCase));

        SetIndex(newIndex >= 0 ? newIndex : (Entries.Count > 0 ? 0 : -1));
    }

    private void AdvanceSlideshow() => GoNext();

    private void GoNext()
    {
        int step = IsSpreadMode ? 2 : 1;
        if (CanMoveBy(step))
        {
            MoveBy(step);
            return;
        }

        bool advanced = BoundaryAction switch
        {
            BoundaryAction.FolderNavigation => TryEnterNextFolder(),
            _ => LoopToStart()
        };

        if (!advanced) IsSlideshowActive = false;
    }

    private void GoPrev()
    {
        int step = IsSpreadMode ? 2 : 1;
        if (CanMoveBy(-step))
        {
            MoveBy(-step);
            return;
        }

        switch (BoundaryAction)
        {
            case BoundaryAction.FolderNavigation:
                TryEnterPreviousFolder();
                break;
            default:
                LoopToEnd();
                break;
        }
    }

    private bool LoopToStart()
    {
        if (Entries.Count == 0) return false;
        SetIndex(0);
        return true;
    }

    private bool LoopToEnd()
    {
        if (Entries.Count == 0) return false;
        SetIndex(Entries.Count - 1);
        return true;
    }

    private bool TryEnterNextFolder()
    {
        if (_folderPath is null) return false;

        string? target = FindNextFolderWithImages(_folderPath);
        if (target is null) return false;

        LoadFolder(target);
        return true;
    }

    private bool TryEnterPreviousFolder()
    {
        if (_folderPath is null) return false;

        string? target = FindPreviousFolderWithImages(_folderPath);
        if (target is null) return false;

        LoadFolder(target);
        if (Entries.Count > 0) SetIndex(Entries.Count - 1);
        return true;
    }

    // 現在フォルダ配下を深さ優先で探索し、画像を含む最初の子階層フォルダを返す。
    private static string? FindNextFolderWithImages(string folderPath)
    {
        List<string> subDirs;
        try
        {
            subDirs = Directory.EnumerateDirectories(folderPath)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return null;
        }

        foreach (var dir in subDirs)
        {
            if (HasImages(dir)) return dir;

            var nested = FindNextFolderWithImages(dir);
            if (nested is not null) return nested;
        }

        return null;
    }

    // 現在フォルダより前(兄弟→親の兄弟…)を遡り、画像を含む最後の子階層フォルダを探す。
    private static string? FindPreviousFolderWithImages(string currentFolder)
    {
        string folder = currentFolder;

        while (true)
        {
            string? parent = Path.GetDirectoryName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (parent is null || !Directory.Exists(parent)) return null;

            List<string> siblings;
            try
            {
                siblings = Directory.EnumerateDirectories(parent)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return null;
            }

            int idx = siblings.FindIndex(p => string.Equals(p, folder, StringComparison.OrdinalIgnoreCase));

            for (int i = idx - 1; i >= 0; i--)
            {
                var found = FindLastFolderWithImages(siblings[i]);
                if (found is not null) return found;
            }

            if (HasImages(parent)) return parent;

            folder = parent;
        }
    }

    // 指定フォルダ自身または配下(深さ優先・逆順)で画像を含む最後のフォルダを探す。
    private static string? FindLastFolderWithImages(string folderPath)
    {
        List<string> subDirs;
        try
        {
            subDirs = Directory.EnumerateDirectories(folderPath)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            subDirs = new List<string>();
        }

        for (int i = subDirs.Count - 1; i >= 0; i--)
        {
            var found = FindLastFolderWithImages(subDirs[i]);
            if (found is not null) return found;
        }

        return HasImages(folderPath) ? folderPath : null;
    }

    private static bool HasImages(string folderPath)
    {
        try
        {
            return Directory.EnumerateFiles(folderPath).Any(FolderImageService.IsSupportedImage);
        }
        catch
        {
            return false;
        }
    }

    // code-behind (RecalculateFit) が自動回転の最適角度を反映するために呼ぶ。
    public void SetAutoRotationAngle(double angle) => RotationAngle = angle;

    private static double NormalizeAngle(double angle)
    {
        angle %= 360;
        return angle < 0 ? angle + 360 : angle;
    }

    private bool CanMoveBy(int delta) => Entries.Count > 0 && CurrentIndex + delta is >= 0 && CurrentIndex + delta < Entries.Count;

    private void MoveBy(int delta) => SetIndex(Math.Clamp(CurrentIndex + delta, 0, Math.Max(0, Entries.Count - 1)));

    private void SetIndex(int index)
    {
        CurrentIndex = index;
        RotationAngle = 0;
        ZoomMultiplier = 1.0;
        _ = UpdateCurrentImagesAsync();
    }

    private async Task UpdateCurrentImagesAsync()
    {
        if (CurrentIndex < 0 || CurrentIndex >= Entries.Count)
        {
            PrimaryImage = null;
            SecondaryImage = null;
            StatusText = "フォルダを開いてください (Ctrl+O)";
            return;
        }

        _cache.PreloadAround(Entries, CurrentIndex);

        var primaryEntry = Entries[CurrentIndex];
        PrimaryImage = await _cache.GetAsync(primaryEntry.FullPath);

        if (IsSpreadMode && CurrentIndex + 1 < Entries.Count)
        {
            var secondaryEntry = Entries[CurrentIndex + 1];
            SecondaryImage = await _cache.GetAsync(secondaryEntry.FullPath);
            StatusText = $"{CurrentIndex + 1}-{CurrentIndex + 2} / {Entries.Count}  {primaryEntry.FileName}";
        }
        else
        {
            SecondaryImage = null;
            StatusText = $"{CurrentIndex + 1} / {Entries.Count}  {primaryEntry.FileName}";
        }

        await RefreshRenderedImagesAsync();
    }

    // --- ズーム倍率を任意の係数で変更(マウス中心ズーム用) ---
    public void ZoomBy(double factor) => ZoomMultiplier = Math.Clamp(ZoomMultiplier * factor, MinZoom, MaxZoom);

    // --- フィルター(明るさ/コントラスト/彩度/シャープネス) ---

    private void ScheduleFilterRefresh()
    {
        _filterDebounceTimer.Stop();
        _filterDebounceTimer.Start();
    }

    private async Task RefreshRenderedImagesAsync()
    {
        var primary = PrimaryImage;
        var secondary = SecondaryImage;

        if (primary is null)
        {
            RenderedPrimaryImage = null;
            RenderedSecondaryImage = null;
            return;
        }

        bool noOp = Brightness == 0 && Contrast == 0 && Saturation == 0 && Sharpness == 0;
        if (noOp)
        {
            RenderedPrimaryImage = primary;
            RenderedSecondaryImage = secondary;
            return;
        }

        double brightness = Brightness, contrast = Contrast, saturation = Saturation, sharpness = Sharpness;
        RenderedPrimaryImage = await Task.Run(() => ImageFilterService.Apply(primary, brightness, contrast, saturation, sharpness));
        RenderedSecondaryImage = secondary is null
            ? null
            : await Task.Run(() => ImageFilterService.Apply(secondary, brightness, contrast, saturation, sharpness));
    }

    // --- 設定の保存/復元 ---

    public AppSettings CollectSettings()
    {
        return new AppSettings
        {
            LastFolderPath = _folderPath,
            LastFileName = CurrentIndex >= 0 && CurrentIndex < Entries.Count ? Entries[CurrentIndex].FileName : null,
            SortMode = SortMode,
            SortDescending = SortDescending,
            IsSpreadMode = IsSpreadMode,
            IsRightToLeft = IsRightToLeft,
            IsSidebarVisible = IsSidebarVisible,
            IsAutoRotate = IsAutoRotate,
            IsWheelZoomMode = IsWheelZoomMode,
            BoundaryAction = BoundaryAction,
            FitMode = FitMode,
            SlideshowIntervalSeconds = SlideshowIntervalSeconds,
            FavoriteDirectories = FavoriteDirectories.ToList()
        };
    }

    // フォルダ読み込み前に反映すべき設定を適用する(SortMode等はLoadFolder時に参照されるため)。
    public void ApplySettingsBeforeLoad(AppSettings settings)
    {
        SortMode = settings.SortMode;
        SortDescending = settings.SortDescending;
        IsSpreadMode = settings.IsSpreadMode;
        IsRightToLeft = settings.IsRightToLeft;
        IsSidebarVisible = settings.IsSidebarVisible;
        IsAutoRotate = settings.IsAutoRotate;
        IsWheelZoomMode = settings.IsWheelZoomMode;
        BoundaryAction = settings.BoundaryAction;
        FitMode = settings.FitMode;
        SlideshowIntervalSeconds = settings.SlideshowIntervalSeconds;

        FavoriteDirectories.Clear();
        foreach (var dir in settings.FavoriteDirectories)
        {
            if (Directory.Exists(dir)) FavoriteDirectories.Add(dir);
        }
    }

    // 保存されていた前回のフォルダ/ページを復元する(設定適用後に呼ぶ)。
    public void RestoreLastFolder(AppSettings settings)
    {
        if (settings.LastFolderPath is null || !Directory.Exists(settings.LastFolderPath)) return;

        LoadFolder(settings.LastFolderPath);

        if (settings.LastFileName is not null)
        {
            int index = Entries.ToList().FindIndex(e => string.Equals(e.FileName, settings.LastFileName, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) SetIndex(index);
        }
    }

    // --- 更新チェック ---

    public async Task CheckForUpdatesAsync()
    {
        if (!Version.TryParse(AppVersion, out var current)) return;

        var result = await UpdateCheckService.CheckAsync(current);
        LatestVersion = result.LatestVersion;
        UpdateUrl = result.ReleaseUrl;
        IsUpdateAvailable = result.IsNewer;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
