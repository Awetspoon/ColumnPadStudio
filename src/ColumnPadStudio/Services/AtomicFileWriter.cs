using System.IO;
using System.Text;

namespace ColumnPadStudio.Services;

public static class AtomicFileWriter
{
    public static void WriteText(string path, string content, Encoding? encoding = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var destinationPath = Path.GetFullPath(path);
        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
            Directory.CreateDirectory(destinationDirectory);

        var tempPath = Path.Combine(
            destinationDirectory ?? Environment.CurrentDirectory,
            $"{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(tempPath, content, encoding ?? Encoding.UTF8);
            File.Move(tempPath, destinationPath, overwrite: true);
        }
        finally
        {
            TryDeleteTempFile(tempPath);
        }
    }

    private static void TryDeleteTempFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort temp cleanup only; the original write failure should stay visible.
        }
    }
}
