using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;
using ColumnPadStudio.Domain.Enums;
using ColumnPadStudio.Domain.Logic;
using ColumnPadStudio.Domain.Models;

namespace ColumnPadStudio.Application.Services;

public static class WorkspaceImportService
{
    public static WorkspaceDocument ImportWorkspace(string path, string content, WorkspaceFileKind kind)
    {
        var workspace = kind switch
        {
            WorkspaceFileKind.RawText => BuildRawWorkspace(path, content, WorkspaceFileKind.RawText),
            WorkspaceFileKind.RawMarkdown => BuildRawWorkspace(path, content, WorkspaceFileKind.RawMarkdown),
            WorkspaceFileKind.TextExport => ParseTextExport(path, content),
            WorkspaceFileKind.MarkdownExport => ParseMarkdownExport(path, content),
            WorkspaceFileKind.Layout => LoadLayout(content),
            _ => throw new InvalidOperationException($"Unsupported workspace file kind: {kind}")
        };

        return WorkspaceDocumentLogic.Normalize(workspace);
    }

    public static WorkspaceSessionDocument ImportSession(string content)
    {
        var sessionNode = ParseJsonNode(content);
        if (TryConvertLegacySession(sessionNode, out var legacySession))
        {
            return NormalizeImportedSession(legacySession, workspaceNodes: null);
        }

        var workspaceNodes = GetArrayProperty(sessionNode, "workspaces");
        NormalizeLegacyThemeNames(workspaceNodes);

        var session = JsonSerializer.Deserialize<WorkspaceSessionDocument>(sessionNode?.ToJsonString(WriteOptions()) ?? content, ReadOptions()) ?? new WorkspaceSessionDocument();
        return NormalizeImportedSession(session, workspaceNodes);
    }

    public static string SerializeLayout(WorkspaceDocument workspace)
        => JsonSerializer.Serialize(workspace, WriteOptions());

    private static WorkspaceDocument LoadLayout(string content)
    {
        var rootNode = ParseJsonNode(content);
        if (TryConvertLegacyLayout(rootNode, out var legacyWorkspace))
        {
            ApplyLegacyMigration(legacyWorkspace, legacyWorkspace.Version);
            return WorkspaceDocumentLogic.Normalize(legacyWorkspace);
        }

        NormalizeLegacyThemeNames(rootNode);
        var version = GetWorkspaceVersion(rootNode);
        var workspace = JsonSerializer.Deserialize<WorkspaceDocument>(rootNode?.ToJsonString(WriteOptions()) ?? content, ReadOptions()) ?? WorkspaceDocumentLogic.CreateDefaultWorkspace();
        ApplyLegacyMigration(workspace, version);
        return WorkspaceDocumentLogic.Normalize(workspace);
    }

    private static WorkspaceDocument BuildRawWorkspace(string path, string content, WorkspaceFileKind kind)
    {
        return WorkspaceDocumentLogic.Normalize(new WorkspaceDocument
        {
            Version = LegacyLayoutMigrationLogic.CurrentLayoutVersion,
            Name = Path.GetFileNameWithoutExtension(path),
            Columns = new List<ColumnDocument>
            {
                new()
                {
                    Title = Path.GetFileNameWithoutExtension(path),
                    Text = NormalizeEditorText(content),
                    MarkerMode = MarkerMode.Numbers
                }
            }
        });
    }

    private static WorkspaceDocument ParseTextExport(string path, string content)
    {
        var workspace = WorkspaceDocumentLogic.CreateDefaultWorkspace(Path.GetFileNameWithoutExtension(path));
        workspace.Columns = ParseSections(content, line => line.StartsWith("=====", StringComparison.Ordinal), header => header.Trim('=').Trim());
        return WorkspaceDocumentLogic.Normalize(workspace);
    }

    private static WorkspaceDocument ParseMarkdownExport(string path, string content)
    {
        var workspace = WorkspaceDocumentLogic.CreateDefaultWorkspace(Path.GetFileNameWithoutExtension(path));
        workspace.Columns = ParseSections(content, line => line.StartsWith("## ", StringComparison.Ordinal), header => header[3..].Trim());
        return WorkspaceDocumentLogic.Normalize(workspace);
    }

    private static List<ColumnDocument> ParseSections(string content, Func<string, bool> isHeader, Func<string, string> getTitle)
    {
        var lines = NormalizeEditorText(content).Split('\n');
        var columns = new List<ColumnDocument>();
        var body = new StringBuilder();
        var title = string.Empty;
        var foundHeader = false;

        void Flush()
        {
            if (!foundHeader && body.Length == 0)
            {
                return;
            }

            columns.Add(new ColumnDocument
            {
                Title = string.IsNullOrWhiteSpace(title) ? $"Column {columns.Count + 1}" : title,
                Text = body.ToString().TrimEnd('\n'),
                MarkerMode = MarkerMode.Numbers
            });
            body.Clear();
        }

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (isHeader(line))
            {
                if (foundHeader)
                {
                    Flush();
                }

                foundHeader = true;
                title = getTitle(line);

                if (index + 1 < lines.Length && string.IsNullOrWhiteSpace(lines[index + 1]))
                {
                    index++;
                }

                continue;
            }

            if (body.Length > 0)
            {
                body.Append('\n');
            }
            body.Append(line);
        }

        if (foundHeader)
        {
            Flush();
        }

        if (columns.Count == 0)
        {
            columns.Add(new ColumnDocument
            {
                Title = "Column 1",
                Text = NormalizeEditorText(content),
                MarkerMode = MarkerMode.Numbers
            });
        }

        return columns;
    }

    private static string NormalizeEditorText(string content)
    {
        return content.Replace("\r\r\n", "\n")
            .Replace("\u2028", "\n")
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');
    }

    private static void ApplyLegacyMigration(WorkspaceDocument workspace, int version)
    {
        if (workspace.Columns.Count == 0)
        {
            workspace.Columns.Add(new ColumnDocument { Title = "Column 1" });
        }

        if (version < 14 && workspace.Defaults.LinedPaper && workspace.Defaults.LineStyle == EditorLineStyle.StandardRuled)
        {
            workspace.Defaults.LineStyle = EditorLineStyle.LegacyRuled;
        }

        if (version < LegacyLayoutMigrationLogic.CurrentLayoutVersion)
        {
            foreach (var column in workspace.Columns)
            {
                var normalized = LegacyLayoutMigrationLogic.NormalizeLegacyText(column.Text);
                normalized = LegacyLayoutMigrationLogic.ExpandEscapedNewlinesWhenSingleLine(normalized);
                normalized = LegacyLayoutMigrationLogic.NormalizeLegacyText(normalized);

                if (!normalized.Contains('\n') && !normalized.Contains('\r'))
                {
                    normalized = LegacyLayoutMigrationLogic.MigrateLegacyInlineText(normalized, column.Width, column.FontSize);
                }

                column.Text = LegacyLayoutMigrationLogic.NormalizeLegacyText(normalized);
                LegacyLayoutMigrationLogic.TryMigrateLegacyMarkers(column);
            }
        }
        else
        {
            foreach (var column in workspace.Columns)
            {
                column.Text = LegacyLayoutMigrationLogic.NormalizeLegacyText(column.Text);
            }
        }

        workspace.Version = LegacyLayoutMigrationLogic.CurrentLayoutVersion;
    }

    private static void NormalizeLegacyThemeNames(JsonNode? workspaceNode)
    {
        if (workspaceNode is null)
        {
            return;
        }

        if (workspaceNode is JsonArray workspaceArray)
        {
            foreach (var item in workspaceArray)
            {
                NormalizeLegacyThemeNames(item);
            }

            return;
        }

        if (GetProperty(workspaceNode, "defaults") is not JsonObject defaultsNode ||
            GetProperty(defaultsNode, "themePreset") is not JsonValue themeValue)
        {
            return;
        }

        var themeName = themeValue.TryGetValue<string>(out var rawThemeName) ? rawThemeName : null;
        if (themeName is null)
        {
            return;
        }

        if (TryNormalizeThemePreset(themeName, out var normalized))
        {
            defaultsNode["themePreset"] = (int)normalized;
        }
    }

    private static int GetWorkspaceVersion(JsonNode? workspaceNode)
    {
        if (GetProperty(workspaceNode, "version") is JsonValue versionValue &&
            TryGetIntValue(versionValue, out var version))
        {
            return version;
        }

        return 0;
    }

    private static int GetWorkspaceVersion(JsonArray? workspaceNodes, int index)
    {
        if (workspaceNodes is null || index < 0 || index >= workspaceNodes.Count)
        {
            return 0;
        }

        return GetWorkspaceVersion(workspaceNodes[index]);
    }

    private static JsonNode? ParseJsonNode(string content)
    {
        try
        {
            return JsonNode.Parse(content);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonSerializerOptions ReadOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    private static JsonSerializerOptions WriteOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    private static WorkspaceSessionDocument NormalizeImportedSession(WorkspaceSessionDocument session, JsonArray? workspaceNodes)
    {
        for (var index = 0; index < session.Workspaces.Count; index++)
        {
            var version = workspaceNodes is null
                ? session.Workspaces[index].Version
                : GetWorkspaceVersion(workspaceNodes, index);
            ApplyLegacyMigration(session.Workspaces[index], version);
            WorkspaceDocumentLogic.Normalize(session.Workspaces[index]);
        }

        if (session.Workspaces.Count == 0)
        {
            session.Workspaces.Add(WorkspaceDocumentLogic.CreateDefaultWorkspace());
        }

        session.ActiveWorkspaceIndex = Math.Clamp(session.ActiveWorkspaceIndex, 0, session.Workspaces.Count - 1);
        return session;
    }

    private static bool TryConvertLegacySession(JsonNode? sessionNode, out WorkspaceSessionDocument session)
    {
        session = new WorkspaceSessionDocument();
        var workspaceNodes = GetArrayProperty(sessionNode, "workspaces");
        if (workspaceNodes is null)
        {
            return false;
        }

        var workspaces = new List<WorkspaceDocument>();
        var foundLegacyWrapper = false;

        foreach (var workspaceNode in workspaceNodes)
        {
            if (!TryGetStringProperty(workspaceNode, "layoutJson", out var layoutJson) ||
                string.IsNullOrWhiteSpace(layoutJson))
            {
                continue;
            }

            foundLegacyWrapper = true;
            var workspace = LoadLayout(layoutJson);

            if (TryGetStringProperty(workspaceNode, "name", out var workspaceName) &&
                !string.IsNullOrWhiteSpace(workspaceName))
            {
                workspace.Name = workspaceName.Trim();
            }

            if (TryGetIntProperty(workspaceNode, "lastMultiColumnCount", out var lastMultiColumnCount))
            {
                workspace.LastMultiColumnCount = lastMultiColumnCount;
            }

            workspaces.Add(WorkspaceDocumentLogic.Normalize(workspace));
        }

        if (!foundLegacyWrapper)
        {
            return false;
        }

        session.Workspaces = workspaces;
        session.ActiveWorkspaceIndex = GetIntProperty(sessionNode, "activeWorkspaceIndex", 0);
        return true;
    }

    private static bool TryConvertLegacyLayout(JsonNode? rootNode, out WorkspaceDocument workspace)
    {
        workspace = WorkspaceDocumentLogic.CreateDefaultWorkspace();
        if (!LooksLikeLegacyLayout(rootNode))
        {
            return false;
        }

        var columns = ReadLegacyColumns(GetArrayProperty(rootNode, "columns"));
        var activeColumnIndex = GetIntProperty(rootNode, "activeIndex", -1);
        if (activeColumnIndex < 0 &&
            TryGetGuidProperty(rootNode, "activeId", out var activeColumnId))
        {
            activeColumnIndex = columns.FindIndex(column => column.Id == activeColumnId);
        }

        workspace = new WorkspaceDocument
        {
            Version = GetWorkspaceVersion(rootNode),
            Name = GetStringProperty(rootNode, "name") ?? "Workspace 1",
            ActiveColumnIndex = activeColumnIndex < 0 ? 0 : activeColumnIndex,
            Defaults = new EditorDefaults
            {
                ShowLineNumbers = GetBoolProperty(rootNode, "showLineNumbers", true),
                WordWrap = GetBoolProperty(rootNode, "wordWrap", true),
                FontFamily = GetStringProperty(rootNode, "editorFontFamily") ?? "Consolas",
                FontFaceStyle = GetStringProperty(rootNode, "editorFontStyle") ?? "Regular",
                FontSize = GetDoubleProperty(rootNode, "editorFontSize", 13),
                LineStyle = GetEnumProperty(rootNode, "lineStyle", GetEnumProperty(rootNode, "linedPaperStyle", EditorLineStyle.StandardRuled)),
                ThemePreset = GetThemePresetProperty(rootNode, "themePreset", ThemePreset.Default),
                SpellCheckEnabled = GetBoolProperty(rootNode, "spellCheckEnabled", true),
                LanguageTag = GetStringProperty(rootNode, "editorLanguageTag") ?? "en-US",
                LinedPaper = GetBoolProperty(rootNode, "linedPaperEnabled", false)
            },
            Columns = columns,
            LastMultiColumnCount = columns.Count <= 1 ? 3 : columns.Count
        };

        return true;
    }

    private static bool LooksLikeLegacyLayout(JsonNode? rootNode)
    {
        return GetProperty(rootNode, "showLineNumbers") is not null ||
               GetProperty(rootNode, "wordWrap") is not null ||
               GetProperty(rootNode, "editorFontFamily") is not null ||
               GetProperty(rootNode, "editorFontStyle") is not null ||
               GetProperty(rootNode, "editorFontSize") is not null ||
               GetProperty(rootNode, "lineStyle") is not null ||
               GetProperty(rootNode, "linedPaperStyle") is not null ||
               GetProperty(rootNode, "themePreset") is not null ||
               GetProperty(rootNode, "spellCheckEnabled") is not null ||
               GetProperty(rootNode, "editorLanguageTag") is not null ||
               GetProperty(rootNode, "linedPaperEnabled") is not null ||
               GetProperty(rootNode, "activeIndex") is not null ||
               GetProperty(rootNode, "activeId") is not null;
    }

    private static List<ColumnDocument> ReadLegacyColumns(JsonArray? columnsNode)
    {
        var columns = new List<ColumnDocument>();
        if (columnsNode is null)
        {
            return columns;
        }

        foreach (var columnNode in columnsNode)
        {
            var column = new ColumnDocument
            {
                Title = GetStringProperty(columnNode, "title") ?? "Column",
                Text = GetStringProperty(columnNode, "text") ?? string.Empty,
                Width = GetNullableDoubleProperty(columnNode, "widthPx") ?? GetNullableDoubleProperty(columnNode, "width"),
                IsWidthLocked = GetBoolProperty(columnNode, "isWidthLocked", false),
                PastePreset = GetEnumProperty(columnNode, "pastePreset", PastePreset.None),
                UseDefaultFont = GetBoolProperty(columnNode, "useDefaultFont", true),
                FontFamily = GetStringProperty(columnNode, "fontFamily") ?? "Consolas",
                FontSize = GetDoubleProperty(columnNode, "fontSize", 13),
                FontStyleName = GetStringProperty(columnNode, "fontStyleName") ?? GetStringProperty(columnNode, "fontStyle") ?? "Normal",
                FontWeightName = GetStringProperty(columnNode, "fontWeightName") ?? GetStringProperty(columnNode, "fontWeight") ?? "Normal",
                MarkerMode = GetEnumProperty(columnNode, "lineMarkerMode", GetEnumProperty(columnNode, "markerMode", MarkerMode.Numbers)),
                CheckedLines = GetIntSetProperty(columnNode, "checkedChecklistLineIndexes")
            };

            if (column.CheckedLines.Count == 0)
            {
                column.CheckedLines = GetIntSetProperty(columnNode, "checkedLines");
            }

            if (TryGetGuidProperty(columnNode, "id", out var columnId))
            {
                column.Id = columnId;
            }

            columns.Add(column);
        }

        return columns;
    }

    private static JsonNode? GetProperty(JsonNode? node, string propertyName)
    {
        if (node is not JsonObject jsonObject)
        {
            return null;
        }

        foreach (var entry in jsonObject)
        {
            if (string.Equals(entry.Key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value;
            }
        }

        return null;
    }

    private static JsonArray? GetArrayProperty(JsonNode? node, string propertyName)
        => GetProperty(node, propertyName) as JsonArray;

    private static string? GetStringProperty(JsonNode? node, string propertyName)
    {
        if (GetProperty(node, propertyName) is not JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue<string>(out var text))
        {
            return text;
        }

        return null;
    }

    private static bool TryGetStringProperty(JsonNode? node, string propertyName, out string? value)
    {
        value = GetStringProperty(node, propertyName);
        return !string.IsNullOrWhiteSpace(value);
    }

    private static int GetIntProperty(JsonNode? node, string propertyName, int fallback)
    {
        return GetProperty(node, propertyName) is JsonValue value &&
               TryGetIntValue(value, out var result)
            ? result
            : fallback;
    }

    private static bool TryGetIntProperty(JsonNode? node, string propertyName, out int value)
    {
        if (GetProperty(node, propertyName) is JsonValue propertyValue &&
            TryGetIntValue(propertyValue, out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static double GetDoubleProperty(JsonNode? node, string propertyName, double fallback)
    {
        return GetNullableDoubleProperty(node, propertyName) ?? fallback;
    }

    private static double? GetNullableDoubleProperty(JsonNode? node, string propertyName)
    {
        if (GetProperty(node, propertyName) is not JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue<double>(out var doubleValue))
        {
            return doubleValue;
        }

        if (value.TryGetValue<int>(out var intValue))
        {
            return intValue;
        }

        if (value.TryGetValue<string>(out var text) &&
            double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static bool GetBoolProperty(JsonNode? node, string propertyName, bool fallback)
    {
        if (GetProperty(node, propertyName) is not JsonValue value)
        {
            return fallback;
        }

        if (value.TryGetValue<bool>(out var boolValue))
        {
            return boolValue;
        }

        if (value.TryGetValue<string>(out var text) &&
            bool.TryParse(text, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private static HashSet<int> GetIntSetProperty(JsonNode? node, string propertyName)
    {
        var values = new HashSet<int>();
        if (GetArrayProperty(node, propertyName) is not JsonArray array)
        {
            return values;
        }

        foreach (var item in array)
        {
            if (item is JsonValue value &&
                TryGetIntValue(value, out var intValue))
            {
                values.Add(intValue);
            }
        }

        return values;
    }

    private static TEnum GetEnumProperty<TEnum>(JsonNode? node, string propertyName, TEnum fallback)
        where TEnum : struct, Enum
    {
        if (GetProperty(node, propertyName) is not JsonValue value)
        {
            return fallback;
        }

        if (value.TryGetValue<string>(out var text) &&
            Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        if (TryGetIntValue(value, out var numericValue) &&
            Enum.IsDefined(typeof(TEnum), numericValue))
        {
            return (TEnum)Enum.ToObject(typeof(TEnum), numericValue);
        }

        return fallback;
    }

    private static ThemePreset GetThemePresetProperty(JsonNode? node, string propertyName, ThemePreset fallback)
    {
        if (GetProperty(node, propertyName) is not JsonValue value)
        {
            return fallback;
        }

        if (TryGetIntValue(value, out var numericValue) &&
            Enum.IsDefined(typeof(ThemePreset), numericValue))
        {
            return (ThemePreset)numericValue;
        }

        if (value.TryGetValue<string>(out var text) &&
            TryNormalizeThemePreset(text, out var themePreset))
        {
            return themePreset;
        }

        return fallback;
    }

    private static bool TryGetGuidProperty(JsonNode? node, string propertyName, out Guid value)
    {
        value = Guid.Empty;
        if (GetProperty(node, propertyName) is not JsonValue propertyValue ||
            !propertyValue.TryGetValue<string>(out var text))
        {
            return false;
        }

        return Guid.TryParse(text, out value);
    }

    private static bool TryGetIntValue(JsonValue value, out int result)
    {
        if (value.TryGetValue<int>(out result))
        {
            return true;
        }

        if (value.TryGetValue<double>(out var doubleValue) &&
            doubleValue >= int.MinValue &&
            doubleValue <= int.MaxValue)
        {
            result = (int)doubleValue;
            return true;
        }

        if (value.TryGetValue<string>(out var text) &&
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
        {
            return true;
        }

        result = 0;
        return false;
    }

    private static bool TryNormalizeThemePreset(string? themeName, out ThemePreset themePreset)
    {
        themePreset = ThemePreset.Default;
        if (string.IsNullOrWhiteSpace(themeName))
        {
            return false;
        }

        switch (themeName.Trim())
        {
            case "Notepad Classic":
            case "Light Mode":
            case "Light":
                themePreset = ThemePreset.Light;
                return true;
            case "High Contrast":
            case "Dark Mode":
            case "Dark":
                themePreset = ThemePreset.Dark;
                return true;
            case "Compact":
            case "Default Mode":
            case "Default":
                themePreset = ThemePreset.Default;
                return true;
            default:
                return false;
        }
    }
}
