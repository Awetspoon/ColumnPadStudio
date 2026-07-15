using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using ColumnPadStudio.Domain.Workspaces;
using ColumnPadStudio.Models;

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
            normalized.Add(new WorkspaceSessionFileEntry(
                Name: name,
                Layout: ParseLayoutNode(workspace.LayoutJson),
                LastMultiColumnCount: workspace.LastMultiColumnCount));
        }

        var session = new WorkspaceSessionFile(
            FileType: "ColumnPadWorkspaceSession",
            Version: 2,
            ActiveWorkspaceIndex: Math.Clamp(activeWorkspaceIndex, 0, normalized.Count - 1),
            Workspaces: normalized);

        return JsonSerializer.Serialize(session, JsonOptions);
    }

    public static bool TryParseSession(string? json, out WorkspaceSessionData session)
    {
        session = new WorkspaceSessionData(0, Array.Empty<WorkspaceSessionEntryData>());

        if (string.IsNullOrWhiteSpace(json))
            return false;

        JsonObject? parsed;
        try
        {
            parsed = JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return false;
        }

        if (parsed?["Workspaces"] is not JsonArray workspaceNodes || workspaceNodes.Count == 0)
            return false;

        var workspaces = new List<WorkspaceSessionEntryData>(workspaceNodes.Count);
        for (var i = 0; i < workspaceNodes.Count; i++)
        {
            if (workspaceNodes[i] is not JsonObject entry)
                return false;

            var layoutJson = GetLayoutJson(entry);
            if (string.IsNullOrWhiteSpace(layoutJson))
                return false;

            var name = GetString(entry, "Name", $"Workspace {i + 1}");
            workspaces.Add(new WorkspaceSessionEntryData(
                Name: string.IsNullOrWhiteSpace(name) ? $"Workspace {i + 1}" : name.Trim(),
                LayoutJson: layoutJson,
                LastMultiColumnCount: Math.Max(2, GetInt(entry, "LastMultiColumnCount", 2))));
        }

        session = new WorkspaceSessionData(
            ActiveWorkspaceIndex: Math.Clamp(GetInt(parsed, "ActiveWorkspaceIndex", 0), 0, workspaces.Count - 1),
            Workspaces: workspaces);
        return true;
    }

    private static JsonNode ParseLayoutNode(string? layoutJson)
    {
        if (string.IsNullOrWhiteSpace(layoutJson))
            throw new ArgumentException("Workspace layout JSON cannot be empty.", nameof(layoutJson));

        try
        {
            return JsonNode.Parse(layoutJson) ??
                   throw new ArgumentException("Workspace layout JSON cannot be empty.", nameof(layoutJson));
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("Workspace layout JSON must be valid JSON.", nameof(layoutJson), ex);
        }
    }

    private static string? GetLayoutJson(JsonObject entry)
    {
        if (entry["Layout"] is JsonNode layoutNode)
            return layoutNode.ToJsonString(JsonOptions);

        return GetString(entry, "LayoutJson", null);
    }

    private static string? GetString(JsonObject node, string propertyName, string? fallback)
    {
        return node[propertyName] is JsonValue valueNode &&
               valueNode.TryGetValue<string>(out var parsed)
            ? parsed
            : fallback;
    }

    private static int GetInt(JsonObject node, string propertyName, int fallback)
    {
        return node[propertyName] is JsonValue valueNode &&
               valueNode.TryGetValue<int>(out var parsed)
            ? parsed
            : fallback;
    }

    private sealed record WorkspaceSessionFile(
        string FileType,
        int Version,
        int ActiveWorkspaceIndex,
        List<WorkspaceSessionFileEntry> Workspaces);

    private sealed record WorkspaceSessionFileEntry(
        string Name,
        JsonNode Layout,
        int LastMultiColumnCount);
}
