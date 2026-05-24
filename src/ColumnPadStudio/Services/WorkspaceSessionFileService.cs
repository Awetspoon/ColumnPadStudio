using System.IO;
using System.Linq;
using System.Text.Json;
using ColumnPadStudio.Domain.Workspaces;
using ColumnPadStudio.ViewModels;

namespace ColumnPadStudio.Services;

public sealed record WorkspaceSessionEntryData(string Name, string LayoutJson, int LastMultiColumnCount);

public sealed record WorkspaceSessionData(int ActiveWorkspaceIndex, IReadOnlyList<WorkspaceSessionEntryData> Workspaces);

public sealed record WorkspaceSessionSaveCandidate(
    string? CurrentFilePath,
    SaveFileKind CurrentFileKind,
    bool RequiresSaveAsBeforeOverwrite);

public static class WorkspaceSessionFileService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static bool ShouldSaveWorkspaceSession(IReadOnlyList<WorkspaceSessionSaveCandidate> workspaces)
    {
        ArgumentNullException.ThrowIfNull(workspaces);

        if (workspaces.Count == 0)
            return false;

        if (workspaces.Count > 1)
            return true;

        return IsExistingWorkspaceSessionFile(GetDirectWorkspaceSessionPath(workspaces));
    }

    public static string? GetDirectWorkspaceSessionPath(IReadOnlyList<WorkspaceSessionSaveCandidate> workspaces)
    {
        ArgumentNullException.ThrowIfNull(workspaces);

        if (workspaces.Count == 0)
            return null;

        var distinctPaths = workspaces
            .Select(workspace => workspace.CurrentFilePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinctPaths.Count != 1)
            return null;

        if (workspaces.Any(workspace =>
                workspace.CurrentFileKind != SaveFileKind.Layout ||
                workspace.RequiresSaveAsBeforeOverwrite))
        {
            return null;
        }

        return distinctPaths[0];
    }

    public static bool IsExistingWorkspaceSessionFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        try
        {
            return IsWorkspaceSessionJson(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static bool IsWorkspaceSessionJson(string? json)
    {
        return WorkspaceImportRules.IsWorkspaceSessionJson(json);
    }

    public static string SerializeSession(IReadOnlyList<WorkspaceSessionEntryData> workspaces, int activeWorkspaceIndex)
    {
        ArgumentNullException.ThrowIfNull(workspaces);

        if (workspaces.Count == 0)
            throw new ArgumentException("At least one workspace is required.", nameof(workspaces));

        var normalized = new List<WorkspaceSessionFileEntry>(workspaces.Count);
        for (var i = 0; i < workspaces.Count; i++)
        {
            var workspace = workspaces[i];
            var name = string.IsNullOrWhiteSpace(workspace.Name) ? $"Workspace {i + 1}" : workspace.Name.Trim();
            normalized.Add(new WorkspaceSessionFileEntry(name, workspace.LayoutJson ?? string.Empty, workspace.LastMultiColumnCount));
        }

        var session = new WorkspaceSessionFile(
            Version: 1,
            ActiveWorkspaceIndex: Math.Clamp(activeWorkspaceIndex, 0, normalized.Count - 1),
            Workspaces: normalized);

        return JsonSerializer.Serialize(session, JsonOptions);
    }

    public static bool TryParseSession(string? json, out WorkspaceSessionData session)
    {
        session = new WorkspaceSessionData(0, Array.Empty<WorkspaceSessionEntryData>());

        if (string.IsNullOrWhiteSpace(json))
            return false;

        WorkspaceSessionFile? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<WorkspaceSessionFile>(json);
        }
        catch (JsonException)
        {
            return false;
        }

        if (parsed?.Workspaces is null || parsed.Workspaces.Count == 0)
            return false;

        var workspaces = new List<WorkspaceSessionEntryData>(parsed.Workspaces.Count);
        for (var i = 0; i < parsed.Workspaces.Count; i++)
        {
            var entry = parsed.Workspaces[i];
            if (string.IsNullOrWhiteSpace(entry.LayoutJson))
                return false;

            var name = string.IsNullOrWhiteSpace(entry.Name) ? $"Workspace {i + 1}" : entry.Name.Trim();
            workspaces.Add(new WorkspaceSessionEntryData(
                Name: name,
                LayoutJson: entry.LayoutJson,
                LastMultiColumnCount: Math.Max(2, entry.LastMultiColumnCount)));
        }

        session = new WorkspaceSessionData(
            ActiveWorkspaceIndex: Math.Clamp(parsed.ActiveWorkspaceIndex, 0, workspaces.Count - 1),
            Workspaces: workspaces);
        return true;
    }

    private sealed record WorkspaceSessionFile(
        int Version,
        int ActiveWorkspaceIndex,
        List<WorkspaceSessionFileEntry> Workspaces);

    private sealed record WorkspaceSessionFileEntry(
        string Name,
        string LayoutJson,
        int LastMultiColumnCount);
}
