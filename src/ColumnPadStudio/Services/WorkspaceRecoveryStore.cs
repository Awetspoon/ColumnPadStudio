using System.IO;
using System.Text.Json;
using ColumnPadStudio.Models;

namespace ColumnPadStudio.Services;

public sealed record WorkspaceRecoveryWorkspace(
    string Name,
    string LayoutJson,
    string? CurrentFilePath,
    SaveFileKind CurrentFileKind,
    bool IsDirty,
    bool RequiresSaveAsBeforeOverwrite,
    int LastMultiColumnCount = 3,
    bool HasSessionChanges = false);

public sealed record WorkspaceRecoverySnapshot(
    DateTime SavedUtc,
    int ActiveWorkspaceIndex,
    IReadOnlyList<WorkspaceRecoveryWorkspace> Workspaces);

public static class WorkspaceRecoveryStore
{
    private const string RecoveryFileType = "ColumnPadRecovery";
    private const int CurrentManifestVersion = 2;
    private const string CurrentGenerationFileName = "current-generation.txt";
    private const string GenerationPrefix = "generation-";
    private const string PendingPrefix = ".pending-";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string RecoveryDirectory => AppStoragePaths.RecoveryDirectory;

    public static void Save(
        IReadOnlyList<WorkspaceRecoveryWorkspace> workspaces,
        int activeWorkspaceIndex,
        string? recoveryDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspaces);
        cancellationToken.ThrowIfCancellationRequested();

        if (workspaces.Count == 0)
        {
            Clear(recoveryDirectory, cancellationToken);
            return;
        }

        if (workspaces.Count > WorkspaceSessionFileService.MaxWorkspaces)
        {
            throw new ArgumentException(
                $"Recovery can contain up to {WorkspaceSessionFileService.MaxWorkspaces} workspaces.",
                nameof(workspaces));
        }

        var root = GetRecoveryDirectory(recoveryDirectory);
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(root);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedActiveIndex = Math.Clamp(activeWorkspaceIndex, 0, workspaces.Count - 1);
        var generationId = $"{DateTime.UtcNow:yyyyMMddHHmmssfffffff}-{Guid.NewGuid():N}";
        var generationName = GenerationPrefix + generationId;
        var pendingDirectory = Path.Combine(root, PendingPrefix + generationId);
        var generationDirectory = Path.Combine(root, generationName);
        var manifestEntries = new List<RecoveryManifestWorkspace>(workspaces.Count);
        var pointerActivated = false;

        try
        {
            Directory.CreateDirectory(pendingDirectory);
            cancellationToken.ThrowIfCancellationRequested();
            for (var i = 0; i < workspaces.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var workspace = workspaces[i];
                var fileName = $"workspace-{i + 1}.columnpad.json";

                AtomicFileWriter.WriteText(Path.Combine(pendingDirectory, fileName), workspace.LayoutJson);
                cancellationToken.ThrowIfCancellationRequested();
                manifestEntries.Add(new RecoveryManifestWorkspace(
                    Name: string.IsNullOrWhiteSpace(workspace.Name) ? $"Workspace {i + 1}" : workspace.Name.Trim(),
                    FileName: fileName,
                    CurrentFilePath: workspace.CurrentFilePath,
                    CurrentFileKind: workspace.CurrentFileKind.ToString(),
                    IsDirty: workspace.IsDirty,
                    RequiresSaveAsBeforeOverwrite: workspace.RequiresSaveAsBeforeOverwrite,
                    LastMultiColumnCount: workspace.LastMultiColumnCount,
                    HasSessionChanges: workspace.HasSessionChanges));
            }

            var manifest = new RecoveryManifest(
                FileType: RecoveryFileType,
                Version: CurrentManifestVersion,
                SavedUtc: DateTime.UtcNow,
                ActiveWorkspaceIndex: normalizedActiveIndex,
                Workspaces: manifestEntries);

            cancellationToken.ThrowIfCancellationRequested();
            var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
            cancellationToken.ThrowIfCancellationRequested();
            AtomicFileWriter.WriteText(
                Path.Combine(pendingDirectory, "manifest.json"),
                manifestJson);
            cancellationToken.ThrowIfCancellationRequested();

            // A cancelled write must never activate an incomplete generation.
            Directory.Move(pendingDirectory, generationDirectory);
            cancellationToken.ThrowIfCancellationRequested();
            AtomicFileWriter.WriteText(Path.Combine(root, CurrentGenerationFileName), generationName);
            pointerActivated = true;
            CleanupOldGenerations(root, generationDirectory);
        }
        finally
        {
            TryDeleteDirectory(pendingDirectory);
            if (!pointerActivated)
                TryDeleteDirectory(generationDirectory);
        }
    }

    public static bool TryLoad(out WorkspaceRecoverySnapshot snapshot, string? recoveryDirectory = null)
    {
        snapshot = new WorkspaceRecoverySnapshot(DateTime.MinValue, 0, Array.Empty<WorkspaceRecoveryWorkspace>());

        var root = GetRecoveryDirectory(recoveryDirectory);
        if (!Directory.Exists(root))
            return false;

        foreach (var generationDirectory in GetGenerationCandidates(root))
        {
            if (TryLoadSnapshot(generationDirectory, requireCompleteSnapshot: true, out snapshot))
                return true;
        }

        // Version 1 stored its manifest and workspace files directly in the root.
        return TryLoadSnapshot(root, requireCompleteSnapshot: false, out snapshot);
    }

    public static void Clear(
        string? recoveryDirectory = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = GetRecoveryDirectory(recoveryDirectory);
        cancellationToken.ThrowIfCancellationRequested();
        if (Directory.Exists(root))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Delete(root, recursive: true);
        }
    }

    public static bool TryClear(
        string? recoveryDirectory = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Clear(recoveryDirectory, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string GetRecoveryDirectory(string? recoveryDirectory)
    {
        return string.IsNullOrWhiteSpace(recoveryDirectory) ? RecoveryDirectory : recoveryDirectory;
    }

    private static IEnumerable<string> GetGenerationCandidates(string root)
    {
        var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pointerPath = Path.Combine(root, CurrentGenerationFileName);
        string? pointedGenerationPath = null;

        try
        {
            if (File.Exists(pointerPath))
            {
                var generationName = File.ReadAllText(pointerPath).Trim();
                if (IsSafeGenerationName(generationName))
                {
                    var generationPath = Path.Combine(root, generationName);
                    if (Directory.Exists(generationPath))
                        pointedGenerationPath = generationPath;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Fall through to complete generations already present on disk.
        }

        if (pointedGenerationPath is not null && yielded.Add(pointedGenerationPath))
            yield return pointedGenerationPath;

        IReadOnlyList<string> generationDirectories;
        try
        {
            generationDirectories = Directory
                .GetDirectories(root, GenerationPrefix + "*")
                .OrderByDescending(Directory.GetLastWriteTimeUtc)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            generationDirectories = Array.Empty<string>();
        }

        foreach (var generationDirectory in generationDirectories)
        {
            if (yielded.Add(generationDirectory))
                yield return generationDirectory;
        }
    }

    private static bool TryLoadSnapshot(
        string directory,
        bool requireCompleteSnapshot,
        out WorkspaceRecoverySnapshot snapshot)
    {
        snapshot = new WorkspaceRecoverySnapshot(DateTime.MinValue, 0, Array.Empty<WorkspaceRecoveryWorkspace>());
        var manifestPath = Path.Combine(directory, "manifest.json");
        if (!File.Exists(manifestPath))
            return false;

        try
        {
            var manifest = JsonSerializer.Deserialize<RecoveryManifest>(File.ReadAllText(manifestPath));
            if (manifest?.Workspaces is null ||
                manifest.Workspaces.Count == 0 ||
                manifest.Workspaces.Count > WorkspaceSessionFileService.MaxWorkspaces)
            {
                return false;
            }

            if (manifest.Version < 1 || manifest.Version > CurrentManifestVersion)
                return false;

            if (manifest.Version >= CurrentManifestVersion &&
                !string.Equals(manifest.FileType, RecoveryFileType, StringComparison.Ordinal))
            {
                return false;
            }

            var workspaces = new List<WorkspaceRecoveryWorkspace>(manifest.Workspaces.Count);
            foreach (var entry in manifest.Workspaces)
            {
                if (!IsSafeWorkspaceFileName(entry.FileName))
                    return false;

                var filePath = Path.Combine(directory, entry.FileName);
                if (!File.Exists(filePath))
                {
                    if (requireCompleteSnapshot)
                        return false;

                    continue;
                }

                var layoutJson = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(layoutJson))
                {
                    if (requireCompleteSnapshot)
                        return false;

                    continue;
                }

                var retiredMarkdownFileKind = string.Equals(entry.CurrentFileKind, "MarkdownDocument", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(entry.CurrentFileKind, "MarkdownExport", StringComparison.OrdinalIgnoreCase);
                var kind = !retiredMarkdownFileKind &&
                           Enum.TryParse<SaveFileKind>(entry.CurrentFileKind, ignoreCase: true, out var parsedKind) &&
                           Enum.IsDefined(parsedKind)
                    ? parsedKind
                    : SaveFileKind.Layout;

                workspaces.Add(new WorkspaceRecoveryWorkspace(
                    Name: string.IsNullOrWhiteSpace(entry.Name) ? $"Workspace {workspaces.Count + 1}" : entry.Name.Trim(),
                    LayoutJson: layoutJson,
                    CurrentFilePath: retiredMarkdownFileKind ? null : entry.CurrentFilePath,
                    CurrentFileKind: kind,
                    IsDirty: entry.IsDirty,
                    RequiresSaveAsBeforeOverwrite: retiredMarkdownFileKind ? false : entry.RequiresSaveAsBeforeOverwrite,
                    LastMultiColumnCount: Math.Max(2, entry.LastMultiColumnCount),
                    HasSessionChanges: entry.HasSessionChanges));
            }

            if (workspaces.Count == 0)
                return false;

            snapshot = new WorkspaceRecoverySnapshot(
                SavedUtc: manifest.SavedUtc,
                ActiveWorkspaceIndex: Math.Clamp(manifest.ActiveWorkspaceIndex, 0, workspaces.Count - 1),
                Workspaces: workspaces);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsSafeGenerationName(string generationName)
    {
        return generationName.StartsWith(GenerationPrefix, StringComparison.Ordinal) &&
               string.Equals(Path.GetFileName(generationName), generationName, StringComparison.Ordinal);
    }

    private static bool IsSafeWorkspaceFileName(string? fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName) &&
               string.Equals(Path.GetFileName(fileName), fileName, StringComparison.OrdinalIgnoreCase) &&
               fileName.StartsWith("workspace-", StringComparison.OrdinalIgnoreCase) &&
               fileName.EndsWith(".columnpad.json", StringComparison.OrdinalIgnoreCase);
    }

    private static void CleanupOldGenerations(string root, string currentGenerationDirectory)
    {
        var generationDirectories = Directory
            .GetDirectories(root, GenerationPrefix + "*")
            .OrderByDescending(Directory.GetLastWriteTimeUtc)
            .ToList();

        var retained = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            currentGenerationDirectory
        };

        var previousGeneration = generationDirectories
            .FirstOrDefault(path => !string.Equals(path, currentGenerationDirectory, StringComparison.OrdinalIgnoreCase));
        if (previousGeneration is not null)
            retained.Add(previousGeneration);

        foreach (var generationDirectory in generationDirectories)
        {
            if (!retained.Contains(generationDirectory))
                TryDeleteDirectory(generationDirectory);
        }

        foreach (var pendingDirectory in Directory.GetDirectories(root, PendingPrefix + "*"))
            TryDeleteDirectory(pendingDirectory);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Recovery cleanup is best effort; valid generations must remain usable.
        }
    }

    private sealed record RecoveryManifest(
        string? FileType,
        int Version,
        DateTime SavedUtc,
        int ActiveWorkspaceIndex,
        List<RecoveryManifestWorkspace> Workspaces);

    private sealed record RecoveryManifestWorkspace(
        string Name,
        string FileName,
        string? CurrentFilePath,
        string CurrentFileKind,
        bool IsDirty,
        bool RequiresSaveAsBeforeOverwrite,
        int LastMultiColumnCount = 3,
        bool HasSessionChanges = false);
}
