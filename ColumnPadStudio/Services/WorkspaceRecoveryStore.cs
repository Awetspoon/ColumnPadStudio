using System.IO;
using System.Text.Json;
using ColumnPadStudio.ViewModels;

namespace ColumnPadStudio.Services;

public sealed record WorkspaceRecoveryWorkspace(
    string Name,
    string LayoutJson,
    string? CurrentFilePath,
    SaveFileKind CurrentFileKind,
    bool IsDirty);

public sealed record WorkspaceRecoverySnapshot(
    DateTime SavedUtc,
    int ActiveWorkspaceIndex,
    IReadOnlyList<WorkspaceRecoveryWorkspace> Workspaces);

public static class WorkspaceRecoveryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string RecoveryDirectory => Path.Combine(MainViewModel.AutoSaveDirectory, "Recovery");

    public static void Save(IReadOnlyList<WorkspaceRecoveryWorkspace> workspaces, int activeWorkspaceIndex, string? recoveryDirectory = null)
    {
        if (workspaces.Count == 0)
        {
            Clear(recoveryDirectory);
            return;
        }

        var root = GetRecoveryDirectory(recoveryDirectory);
        Directory.CreateDirectory(root);

        var normalizedActiveIndex = Math.Clamp(activeWorkspaceIndex, 0, workspaces.Count - 1);
        var manifestEntries = new List<RecoveryManifestWorkspace>(workspaces.Count);
        var writtenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < workspaces.Count; i++)
        {
            var workspace = workspaces[i];
            var fileName = $"workspace-{i + 1}.columnpad.json";
            var filePath = Path.Combine(root, fileName);

            WriteTextAtomically(filePath, workspace.LayoutJson);
            manifestEntries.Add(new RecoveryManifestWorkspace(
                Name: string.IsNullOrWhiteSpace(workspace.Name) ? $"Workspace {i + 1}" : workspace.Name.Trim(),
                FileName: fileName,
                CurrentFilePath: workspace.CurrentFilePath,
                CurrentFileKind: workspace.CurrentFileKind.ToString(),
                IsDirty: workspace.IsDirty));
            writtenFiles.Add(fileName);
        }

        foreach (var staleFilePath in Directory.GetFiles(root, "workspace-*.columnpad.json"))
        {
            var fileName = Path.GetFileName(staleFilePath);
            if (!writtenFiles.Contains(fileName))
                File.Delete(staleFilePath);
        }

        var manifest = new RecoveryManifest(
            Version: 1,
            SavedUtc: DateTime.UtcNow,
            ActiveWorkspaceIndex: normalizedActiveIndex,
            Workspaces: manifestEntries);

        WriteTextAtomically(
            Path.Combine(root, "manifest.json"),
            JsonSerializer.Serialize(manifest, JsonOptions));
    }

    public static bool TryLoad(out WorkspaceRecoverySnapshot snapshot, string? recoveryDirectory = null)
    {
        snapshot = new WorkspaceRecoverySnapshot(DateTime.MinValue, 0, Array.Empty<WorkspaceRecoveryWorkspace>());

        var root = GetRecoveryDirectory(recoveryDirectory);
        var manifestPath = Path.Combine(root, "manifest.json");
        if (!File.Exists(manifestPath))
            return false;

        RecoveryManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<RecoveryManifest>(File.ReadAllText(manifestPath));
        }
        catch (JsonException)
        {
            return false;
        }

        if (manifest is null || manifest.Workspaces.Count == 0)
            return false;

        var workspaces = new List<WorkspaceRecoveryWorkspace>(manifest.Workspaces.Count);
        foreach (var entry in manifest.Workspaces)
        {
            if (string.IsNullOrWhiteSpace(entry.FileName))
                continue;

            var filePath = Path.Combine(root, entry.FileName);
            if (!File.Exists(filePath))
                continue;

            var layoutJson = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(layoutJson))
                continue;

            var kind = Enum.TryParse<SaveFileKind>(entry.CurrentFileKind, ignoreCase: true, out var parsedKind)
                ? parsedKind
                : SaveFileKind.Layout;

            workspaces.Add(new WorkspaceRecoveryWorkspace(
                Name: string.IsNullOrWhiteSpace(entry.Name) ? $"Workspace {workspaces.Count + 1}" : entry.Name.Trim(),
                LayoutJson: layoutJson,
                CurrentFilePath: entry.CurrentFilePath,
                CurrentFileKind: kind,
                IsDirty: entry.IsDirty));
        }

        if (workspaces.Count == 0)
            return false;

        snapshot = new WorkspaceRecoverySnapshot(
            SavedUtc: manifest.SavedUtc,
            ActiveWorkspaceIndex: Math.Clamp(manifest.ActiveWorkspaceIndex, 0, workspaces.Count - 1),
            Workspaces: workspaces);
        return true;
    }

    public static void Clear(string? recoveryDirectory = null)
    {
        var root = GetRecoveryDirectory(recoveryDirectory);
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private static string GetRecoveryDirectory(string? recoveryDirectory)
    {
        return string.IsNullOrWhiteSpace(recoveryDirectory) ? RecoveryDirectory : recoveryDirectory;
    }

    private static void WriteTextAtomically(string path, string content)
    {
        var tempPath = $"{path}.tmp";
        File.WriteAllText(tempPath, content);
        File.Move(tempPath, path, overwrite: true);
    }

    private sealed record RecoveryManifest(
        int Version,
        DateTime SavedUtc,
        int ActiveWorkspaceIndex,
        List<RecoveryManifestWorkspace> Workspaces);

    private sealed record RecoveryManifestWorkspace(
        string Name,
        string FileName,
        string? CurrentFilePath,
        string CurrentFileKind,
        bool IsDirty);
}