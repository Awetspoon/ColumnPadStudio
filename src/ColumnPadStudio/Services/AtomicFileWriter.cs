using System.IO;

namespace ColumnPadStudio.Services;

public static class AtomicFileWriter
{
    public static void WriteText(string path, string content)
    {
        var tempPath = $"{path}.tmp";
        File.WriteAllText(tempPath, content);
        File.Move(tempPath, path, overwrite: true);
    }
}
