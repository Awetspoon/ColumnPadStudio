using System.IO;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColumnPadStudio.Services;

public sealed record ColumnImageImport(
    string FilePath,
    string OriginalFileName,
    double DisplayWidth,
    int PixelWidth,
    int PixelHeight,
    string AssetId,
    byte[] Content);

public sealed record ColumnImageDisplay(
    ImageSource Source,
    int PixelWidth,
    int PixelHeight);

public static class ColumnImageFileService
{
    public const int MaxImageFileBytes = 25 * 1024 * 1024;
    public const long MaxImagePixelCount = 80_000_000;
    public const int MaxImageDimension = 20_000;
    private const int MaxDisplayDecodeDimension = 2000;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".gif",
        ".webp",
        ".tif",
        ".tiff"
    };

    public const string ImageOpenFileFilter =
        "Image Files (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp;*.tif;*.tiff|All files (*.*)|*.*";

    public static ColumnImageImport ImportImage(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new IOException("No image file was selected.");

        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("The selected image file could not be found.", sourcePath);

        var extension = Path.GetExtension(sourcePath);
        if (!SupportedExtensions.Contains(extension))
            throw new NotSupportedException("ColumnPad supports PNG, JPG, BMP, GIF, WEBP, and TIFF images.");

        var fileLength = new FileInfo(sourcePath).Length;
        if (fileLength <= 0 || fileLength > MaxImageFileBytes)
            throw new InvalidDataException($"Pictures must be between 1 byte and {MaxImageFileBytes / (1024 * 1024)} MB.");

        var content = File.ReadAllBytes(sourcePath);
        var (pixelWidth, pixelHeight) = ReadPixelSize(content);
        ValidateDimensions(pixelWidth, pixelHeight);

        var originalFileName = Path.GetFileName(sourcePath);
        var assetId = ComputeAssetId(content);
        var displayWidth = Math.Clamp(pixelWidth > 0 ? pixelWidth : 320.0, 160.0, 900.0);

        return new ColumnImageImport(string.Empty, originalFileName, displayWidth, pixelWidth, pixelHeight, assetId, content);
    }

    public static ColumnImageDisplay? LoadDisplaySource(byte[]? content, string? fallbackPath)
    {
        var resolvedContent = content is { Length: > 0 and <= MaxImageFileBytes }
            ? content
            : TryReadImageContent(fallbackPath);
        if (resolvedContent is null)
            return null;

        try
        {
            var (pixelWidth, pixelHeight) = ReadPixelSize(resolvedContent);
            ValidateDimensions(pixelWidth, pixelHeight);

            using var stream = new MemoryStream(resolvedContent, writable: false);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            if (pixelWidth >= pixelHeight && pixelWidth > MaxDisplayDecodeDimension)
                image.DecodePixelWidth = MaxDisplayDecodeDimension;
            else if (pixelHeight > MaxDisplayDecodeDimension)
                image.DecodePixelHeight = MaxDisplayDecodeDimension;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return new ColumnImageDisplay(image, pixelWidth, pixelHeight);
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or FormatException
            or InvalidOperationException
            or InvalidDataException)
        {
            return null;
        }
    }

    public static byte[]? TryReadImageContent(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            var fileLength = new FileInfo(filePath).Length;
            return fileLength is > 0 and <= MaxImageFileBytes
                ? File.ReadAllBytes(filePath)
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static (int Width, int Height) ReadPixelSize(byte[] content)
    {
        try
        {
            using var stream = new MemoryStream(content, writable: false);
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.DelayCreation,
                BitmapCacheOption.None);

            var frame = decoder.Frames.FirstOrDefault();
            return frame is null ? (0, 0) : (frame.PixelWidth, frame.PixelHeight);
        }
        catch (Exception ex) when (ex is IOException
            or NotSupportedException
            or FormatException
            or InvalidOperationException)
        {
            throw new InvalidDataException("The selected file could not be read as an image.", ex);
        }
    }

    private static void ValidateDimensions(int pixelWidth, int pixelHeight)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0 ||
            pixelWidth > MaxImageDimension ||
            pixelHeight > MaxImageDimension ||
            (long)pixelWidth * pixelHeight > MaxImagePixelCount)
        {
            throw new InvalidDataException(
                $"Picture dimensions must be no larger than {MaxImageDimension:N0} pixels per side or {MaxImagePixelCount:N0} pixels in total.");
        }
    }

    private static string ComputeAssetId(byte[] content)
    {
        return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
    }

}
