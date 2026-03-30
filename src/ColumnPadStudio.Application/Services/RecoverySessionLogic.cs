using ColumnPadStudio.Domain.Models;

namespace ColumnPadStudio.Application.Services;

public static class RecoverySessionLogic
{
    public static bool ShouldPersistSnapshot(WorkspaceSessionDocument session)
    {
        if (session.Workspaces.Count == 0)
        {
            return false;
        }

        if (session.Workspaces.Count > 1)
        {
            return true;
        }

        var workspace = session.Workspaces[0];
        return !MatchesBlankStartupWorkspace(workspace);
    }

    public static bool ShouldOfferRestore(RecoverySnapshot snapshot)
    {
        return snapshot.Session.Workspaces.Count > 0 && ShouldPersistSnapshot(snapshot.Session);
    }

    public static string BuildRestorePrompt(RecoverySnapshot snapshot)
    {
        var count = Math.Max(1, snapshot.Session.Workspaces.Count);
        var timestamp = snapshot.SavedAtLocal == default ? DateTime.Now : snapshot.SavedAtLocal;
        var label = BuildRestoreLabel(snapshot, count);
        var backupTime = timestamp.ToString("dd MMMM yyyy 'at' HH:mm");

        return $"{label}{Environment.NewLine}{Environment.NewLine}Last unsaved backup: {backupTime}.{Environment.NewLine}Restore it now?";
    }

    private static string BuildRestoreLabel(RecoverySnapshot snapshot, int count)
    {
        if (count == 1)
        {
            var workspace = snapshot.Session.Workspaces[0];
            var state = snapshot.WorkspaceStates
                .FirstOrDefault(candidate => candidate.WorkspaceId == workspace.Id)
                ?? snapshot.WorkspaceStates.FirstOrDefault();
            var fileName = Path.GetFileName(state?.FilePath);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return $"ColumnPad found an unsaved backup for {fileName}.";
            }

            return $"ColumnPad found an unsaved backup for {workspace.Name}.";
        }

        return $"ColumnPad found unsaved backups for {count} workspaces.";
    }

    private static bool MatchesBlankStartupWorkspace(WorkspaceDocument workspace)
    {
        var defaultWorkspace = Domain.Logic.WorkspaceDocumentLogic.CreateDefaultWorkspace();
        if (!string.Equals(workspace.Name, defaultWorkspace.Name, StringComparison.Ordinal) ||
            workspace.ActiveColumnIndex != defaultWorkspace.ActiveColumnIndex ||
            workspace.LastMultiColumnCount != defaultWorkspace.LastMultiColumnCount)
        {
            return false;
        }

        var currentDefaults = workspace.Defaults;
        var blankDefaults = defaultWorkspace.Defaults;
        if (!MatchesBlankDefaults(currentDefaults, blankDefaults))
        {
            return false;
        }

        if (workspace.Columns.Count != defaultWorkspace.Columns.Count)
        {
            return false;
        }

        for (var index = 0; index < workspace.Columns.Count; index++)
        {
            var current = workspace.Columns[index];
            var blank = defaultWorkspace.Columns[index];
            if (!MatchesBlankColumn(current, blank))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesBlankDefaults(EditorDefaults current, EditorDefaults blank)
    {
        return string.Equals(current.FontFamily, blank.FontFamily, StringComparison.Ordinal) &&
            string.Equals(current.FontFaceStyle, blank.FontFaceStyle, StringComparison.Ordinal) &&
            current.FontSize == blank.FontSize &&
            current.LineStyle == blank.LineStyle &&
            current.ShowLineNumbers == blank.ShowLineNumbers &&
            current.WordWrap == blank.WordWrap &&
            current.LinedPaper == blank.LinedPaper &&
            current.SpellCheckEnabled == blank.SpellCheckEnabled &&
            string.Equals(current.LanguageTag, blank.LanguageTag, StringComparison.Ordinal) &&
            current.ThemePreset == blank.ThemePreset;
    }

    private static bool MatchesBlankColumn(ColumnDocument current, ColumnDocument blank)
    {
        return string.Equals(current.Title, blank.Title, StringComparison.Ordinal) &&
            string.Equals(current.Text, blank.Text, StringComparison.Ordinal) &&
            current.Width == blank.Width &&
            current.IsWidthLocked == blank.IsWidthLocked &&
            current.PastePreset == blank.PastePreset &&
            current.UseDefaultFont == blank.UseDefaultFont &&
            string.Equals(current.FontFamily, blank.FontFamily, StringComparison.Ordinal) &&
            current.FontSize == blank.FontSize &&
            string.Equals(current.FontStyleName, blank.FontStyleName, StringComparison.Ordinal) &&
            string.Equals(current.FontWeightName, blank.FontWeightName, StringComparison.Ordinal) &&
            current.MarkerMode == blank.MarkerMode &&
            current.CheckedLines.SetEquals(blank.CheckedLines);
    }
}
