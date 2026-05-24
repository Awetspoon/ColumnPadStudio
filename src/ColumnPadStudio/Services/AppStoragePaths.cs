using System.IO;

namespace ColumnPadStudio.Services;

public static class AppStoragePaths
{
    public static string RootDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ColumnPadStudio");

    public static string RecoveryDirectory => Path.Combine(RootDirectory, "Recovery");

    public static string WorkflowsDirectory => Path.Combine(RootDirectory, "Workflows");
}