namespace Hamana.Viewer.Models;

// %AppData%\YmbImageViewer\settings.json に保存する前回起動時の状態。
public sealed class AppSettings
{
    public string? LastFolderPath { get; set; }
    public string? LastFileName { get; set; }

    public SortMode SortMode { get; set; } = SortMode.NameNatural;
    public bool SortDescending { get; set; }

    public bool IsSpreadMode { get; set; }
    public bool IsRightToLeft { get; set; } = true;
    public bool IsSidebarVisible { get; set; }
    public bool IsAutoRotate { get; set; }
    public bool IsWheelZoomMode { get; set; }
    public BoundaryAction BoundaryAction { get; set; } = BoundaryAction.Loop;
    public FitMode FitMode { get; set; } = FitMode.Contain;
    public double SlideshowIntervalSeconds { get; set; } = 3.0;

    public List<string> FavoriteDirectories { get; set; } = new();

    public double WindowWidth { get; set; } = 1280;
    public double WindowHeight { get; set; } = 800;
    public double WindowLeft { get; set; } = -1;
    public double WindowTop { get; set; } = -1;
    public bool WindowMaximized { get; set; }
}
