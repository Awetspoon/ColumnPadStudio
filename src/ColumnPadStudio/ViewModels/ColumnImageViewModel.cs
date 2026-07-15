using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColumnPadStudio.ViewModels;

public sealed class ColumnImageViewModel : NotifyBase
{
    public const double MinDisplayWidth = 80.0;
    public const double MaxDisplayWidth = 2000.0;

    private string _filePath;
    private string _originalFileName;
    private ImageSource? _displaySource;
    private double _width;
    private double _left;
    private double _top;
    private ColumnImageLayer _layer;
    private bool _isSelected;

    public ColumnImageViewModel(
        string filePath,
        string? originalFileName = null,
        double width = 320.0,
        int pixelWidth = 0,
        int pixelHeight = 0,
        double left = 12.0,
        double top = 12.0,
        ColumnImageLayer layer = ColumnImageLayer.InFrontOfText)
    {
        Id = Guid.NewGuid().ToString("N");
        _filePath = filePath;
        _displaySource = LoadDisplaySource(filePath);
        _originalFileName = string.IsNullOrWhiteSpace(originalFileName)
            ? Path.GetFileName(filePath)
            : originalFileName.Trim();
        _width = ClampWidth(width);
        _left = ClampPosition(left);
        _top = ClampPosition(top);
        _layer = layer;
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
            _displaySource = LoadDisplaySource(nextValue);
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(DisplaySource));
            OnPropertyChanged(nameof(CanDisplayImage));
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
            OnPropertyChanged(nameof(Height));
        }
    }

    public double Height => PixelWidth > 0 && PixelHeight > 0
        ? Math.Max(1.0, Width * PixelHeight / PixelWidth)
        : Math.Max(1.0, Width * 0.75);

    public double Left
    {
        get => _left;
        set => Set(ref _left, ClampPosition(value));
    }

    public double Top
    {
        get => _top;
        set => Set(ref _top, ClampPosition(value));
    }

    public ColumnImageLayer Layer
    {
        get => _layer;
        set
        {
            if (_layer == value)
                return;

            _layer = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BehindTextVisibility));
            OnPropertyChanged(nameof(OverlayVisibility));
            OnPropertyChanged(nameof(LayerActionText));
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(OverlayVisibility));
            OnPropertyChanged(nameof(SelectionVisibility));
        }
    }

    public int PixelWidth { get; }
    public int PixelHeight { get; }

    public ImageSource? DisplaySource => _displaySource;

    public string DisplayName => string.IsNullOrWhiteSpace(OriginalFileName)
        ? Path.GetFileName(FilePath)
        : OriginalFileName;

    public bool CanDisplayImage => DisplaySource is not null;

    public Visibility BehindTextVisibility => Layer == ColumnImageLayer.BehindText
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility OverlayVisibility => Layer == ColumnImageLayer.InFrontOfText || IsSelected
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility SelectionVisibility => IsSelected ? Visibility.Visible : Visibility.Collapsed;

    public string LayerActionText => Layer == ColumnImageLayer.InFrontOfText
        ? "Place Behind Text"
        : "Place In Front of Text";

    public ColumnImageViewModel Duplicate()
        => new(FilePath, OriginalFileName, Width, PixelWidth, PixelHeight, Left, Top, Layer);

    private static double ClampWidth(double width)
    {
        if (double.IsNaN(width) || double.IsInfinity(width))
            return 320.0;

        return Math.Clamp(width, MinDisplayWidth, MaxDisplayWidth);
    }

    private static double ClampPosition(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return 0.0;

        return Math.Max(0.0, value);
    }

    private static ImageSource? LoadDisplaySource(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            using var stream = File.OpenRead(filePath);
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            var frame = decoder.Frames.FirstOrDefault();
            frame?.Freeze();
            return frame;
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or FormatException
            or InvalidOperationException)
        {
            return null;
        }
    }
}
