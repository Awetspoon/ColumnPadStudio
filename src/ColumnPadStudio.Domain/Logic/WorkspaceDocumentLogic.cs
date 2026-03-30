using ColumnPadStudio.Domain.Enums;
using ColumnPadStudio.Domain.Models;

namespace ColumnPadStudio.Domain.Logic;

public static class WorkspaceDocumentLogic
{
    private const string DefaultFontFamily = "Consolas";
    private const string DefaultFontFaceStyle = "Regular";
    private const string DefaultLanguageTag = "en-US";
    private const double DefaultFontSize = 13;

    public static WorkspaceDocument CreateDefaultWorkspace(string name = "Workspace 1", string? defaultFontFamily = null)
    {
        var resolvedFontFamily = ResolveFontFamily(defaultFontFamily);
        return Normalize(new WorkspaceDocument
        {
            Version = LegacyLayoutMigrationLogic.CurrentLayoutVersion,
            Name = string.IsNullOrWhiteSpace(name) ? "Workspace 1" : name.Trim(),
            ActiveColumnIndex = 0,
            LastMultiColumnCount = 3,
            Defaults = new EditorDefaults
            {
                FontFamily = resolvedFontFamily,
                FontFaceStyle = DefaultFontFaceStyle,
                FontSize = DefaultFontSize,
                LineStyle = EditorLineStyle.StandardRuled,
                ShowLineNumbers = true,
                WordWrap = true,
                SpellCheckEnabled = true,
                LanguageTag = DefaultLanguageTag,
                ThemePreset = ThemePreset.Default,
                LinedPaper = false
            },
            Columns = new List<ColumnDocument>
            {
                CreateDefaultColumn("Column 1", MarkerMode.Numbers, resolvedFontFamily),
                CreateDefaultColumn("Column 2", MarkerMode.Bullets, resolvedFontFamily),
                CreateDefaultColumn("Column 3", MarkerMode.Checklist, resolvedFontFamily)
            }
        }, resolvedFontFamily);
    }

    public static WorkspaceDocument Normalize(WorkspaceDocument workspace, string? fallbackFontFamily = null)
    {
        workspace ??= new WorkspaceDocument();
        workspace.Name = string.IsNullOrWhiteSpace(workspace.Name) ? "Workspace 1" : workspace.Name.Trim();
        workspace.Defaults ??= new EditorDefaults();

        var resolvedFontFamily = ResolveFontFamily(
            string.IsNullOrWhiteSpace(workspace.Defaults.FontFamily) ? fallbackFontFamily : workspace.Defaults.FontFamily);

        workspace.Defaults.FontFamily = resolvedFontFamily;
        workspace.Defaults.FontFaceStyle = string.IsNullOrWhiteSpace(workspace.Defaults.FontFaceStyle)
            ? DefaultFontFaceStyle
            : workspace.Defaults.FontFaceStyle.Trim();
        workspace.Defaults.FontSize = WorkspaceRules.ClampFontSize(
            workspace.Defaults.FontSize <= 0 ? DefaultFontSize : workspace.Defaults.FontSize);
        workspace.Defaults.LineStyle = Enum.IsDefined(typeof(EditorLineStyle), workspace.Defaults.LineStyle)
            ? workspace.Defaults.LineStyle
            : EditorLineStyle.StandardRuled;
        workspace.Defaults.LanguageTag = string.IsNullOrWhiteSpace(workspace.Defaults.LanguageTag)
            ? DefaultLanguageTag
            : workspace.Defaults.LanguageTag.Trim();
        workspace.LastMultiColumnCount = WorkspaceRules.ClampColumnCount(
            workspace.LastMultiColumnCount <= 1 ? 3 : workspace.LastMultiColumnCount);

        var columns = (workspace.Columns ?? new List<ColumnDocument>())
            .Take(WorkspaceRules.MaxColumns)
            .ToList();

        if (columns.Count == 0)
        {
            columns.Add(CreateDefaultColumn("Column 1", MarkerMode.Numbers, resolvedFontFamily));
        }

        workspace.Columns = columns
            .Select((column, index) => NormalizeColumn(column, index, resolvedFontFamily, workspace.Defaults.FontSize))
            .ToList();
        workspace.ActiveColumnIndex = Math.Clamp(workspace.ActiveColumnIndex, 0, workspace.Columns.Count - 1);
        workspace.Version = Math.Max(workspace.Version, LegacyLayoutMigrationLogic.CurrentLayoutVersion);
        return workspace;
    }

    private static ColumnDocument CreateDefaultColumn(string title, MarkerMode markerMode, string fontFamily)
    {
        return new ColumnDocument
        {
            Title = title,
            Text = string.Empty,
            MarkerMode = markerMode,
            PastePreset = PastePreset.None,
            UseDefaultFont = true,
            FontFamily = fontFamily,
            FontSize = DefaultFontSize,
            FontStyleName = "Normal",
            FontWeightName = "Normal",
            Width = null,
            IsWidthLocked = false,
            CheckedLines = new HashSet<int>()
        };
    }

    private static ColumnDocument NormalizeColumn(ColumnDocument? column, int index, string defaultFontFamily, double defaultFontSize)
    {
        column ??= new ColumnDocument();
        column.Title = string.IsNullOrWhiteSpace(column.Title) ? $"Column {index + 1}" : column.Title.Trim();
        column.Text ??= string.Empty;
        column.Width = ColumnWidthLogic.ClampStoredWidth(column.Width);
        column.FontFamily = ResolveFontFamily(string.IsNullOrWhiteSpace(column.FontFamily) ? defaultFontFamily : column.FontFamily);
        column.FontSize = WorkspaceRules.ClampFontSize(column.FontSize <= 0 ? defaultFontSize : column.FontSize);
        column.FontStyleName = string.IsNullOrWhiteSpace(column.FontStyleName) ? "Normal" : column.FontStyleName.Trim();
        column.FontWeightName = string.IsNullOrWhiteSpace(column.FontWeightName) ? "Normal" : column.FontWeightName.Trim();
        column.CheckedLines ??= new HashSet<int>();

        var lineCount = TextMetrics.GetLineCount(column.Text);
        column.CheckedLines.RemoveWhere(line => line < 0 || line >= lineCount);
        column.CheckedLines = new HashSet<int>(column.CheckedLines.OrderBy(line => line));
        return column;
    }

    private static string ResolveFontFamily(string? fontFamily)
    {
        return string.IsNullOrWhiteSpace(fontFamily) ? DefaultFontFamily : fontFamily.Trim();
    }
}
