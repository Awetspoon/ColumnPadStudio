using System.IO;

namespace ColumnPadStudio.ViewModels;

public sealed class ColumnImageViewModel : NotifyBase
{
    public const double MinDisplayWidth = 80.0;
    public const double MaxDisplayWidth = 2000.0;

    private string _filePath;
    private string _originalFileName;
    private double _width;

    public ColumnImageViewModel(
        string filePath,
        string? originalFileName = null,
        double width = 320.0,
        int pixelWidth = 0,
        int pixelHeight = 0)
    {
        Id = Guid.NewGuid().ToString("N");
        _filePath = filePath;
        _originalFileName = string.IsNullOrWhiteSpace(originalFileName)
            ? Path.GetFileName(filePath)
            : originalFileName.Trim();
        _width = ClampWidth(width);
        PixelWidth = Math.Max(0, pixelWidth);
        PixelHeight = Math.Max(0, pixelHeight);
    }

    public string Id { get; }

    public string FilePath
    {
        get => _filePath;
        set
        {
            var nextValue = value ?? string.Empty;
            if (string.Equals(_filePath, nextValue, StringComparison.Ordinal))
                return;

            _filePath = nextValue;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(FileExists));
        }
    }

    public string OriginalFileName
    {
        get => _originalFileName;
        set
        {
            var nextValue = string.IsNullOrWhiteSpace(value) ? Path.GetFileName(FilePath) : value.Trim();
            if (string.Equals(_originalFileName, nextValue, StringComparison.Ordinal))
                return;

            _originalFileName = nextValue;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public double Width
    {
        get => _width;
        set
        {
            var nextValue = ClampWidth(value);
            if (Math.Abs(_width - nextValue) < 0.001)
                return;

            _width = nextValue;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SizeText));
        }
    }

    public int PixelWidth { get; }
    public int PixelHeight { get; }

    public string DisplayName => string.IsNullOrWhiteSpace(OriginalFileName)
        ? Path.GetFileName(FilePath)
        : OriginalFileName;

    public bool FileExists => !string.IsNullOrWhiteSpace(FilePath) && File.Exists(FilePath);

    public string SizeText => PixelWidth > 0 && PixelHeight > 0
        ? $"{Math.Round(Width):0}px wide | source {PixelWidth} x {PixelHeight}"
        : $"{Math.Round(Width):0}px wide";

    public ColumnImageViewModel Duplicate()
        => new(FilePath, OriginalFileName, Width, PixelWidth, PixelHeight);

    private static double ClampWidth(double width)
    {
        if (double.IsNaN(width) || double.IsInfinity(width))
            return 320.0;

        return Math.Clamp(width, MinDisplayWidth, MaxDisplayWidth);
    }
}
