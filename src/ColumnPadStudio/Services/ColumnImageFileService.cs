using System.IO;
using System.Windows.Media.Imaging;

namespace ColumnPadStudio.Services;

public sealed record ColumnImageImport(
    string FilePath,
    string OriginalFileName,
    double DisplayWidth,
    int PixelWidth,
    int PixelHeight);

public static class ColumnImageFileService
{
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

        Directory.CreateDirectory(AppStoragePaths.ImagesDirectory);

        var originalFileName = Path.GetFileName(sourcePath);
        var safeBaseName = SanitizeFileName(Path.GetFileNameWithoutExtension(sourcePath));
        var storedFileName = $"{safeBaseName}-{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var storedPath = Path.Combine(AppStoragePaths.ImagesDirectory, storedFileName);

        File.Copy(sourcePath, storedPath, overwrite: false);

        var (pixelWidth, pixelHeight) = ReadPixelSize(storedPath);
        var displayWidth = Math.Clamp(pixelWidth > 0 ? pixelWidth : 320.0, 160.0, 900.0);

        return new ColumnImageImport(storedPath, originalFileName, displayWidth, pixelWidth, pixelHeight);
    }

    private static (int Width, int Height) ReadPixelSize(string imagePath)
    {
        try
        {
            using var stream = File.OpenRead(imagePath);
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

            var frame = decoder.Frames.FirstOrDefault();
            return frame is null
                ? (0, 0)
                : (frame.PixelWidth, frame.PixelHeight);
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException)
        {
            throw new InvalidDataException("The selected file could not be read as an image.", ex);
        }
    }

    private static string SanitizeFileName(string? value)
    {
        var name = string.IsNullOrWhiteSpace(value) ? "image" : value.Trim();
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(ch => invalidChars.Contains(ch) ? '-' : ch).ToArray()).Trim('-', ' ');
        return string.IsNullOrWhiteSpace(sanitized) ? "image" : sanitized;
    }
}
