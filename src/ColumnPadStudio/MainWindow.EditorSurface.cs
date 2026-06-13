using ColumnPadStudio.Controls;
using ColumnPadStudio.Domain.Lists;
using ColumnPadStudio.Services;
using ColumnPadStudio.ViewModels;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ColumnPadStudio;

public partial class MainWindow
{
    private void AddColumn_Click(object sender, RoutedEventArgs e)
    {
        ActiveVm.AddColumn();
    }

    private void RemoveActive_Click(object sender, RoutedEventArgs e)
    {
        RemoveActiveWithConfirmation();
    }

    private void MoveActiveLeft_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveVm.MoveActiveColumnLeft())
            GetActiveEditorControl()?.FocusEditor();
    }

    private void MoveActiveRight_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveVm.MoveActiveColumnRight())
            GetActiveEditorControl()?.FocusEditor();
    }

    private void ResetWidths_Click(object sender, RoutedEventArgs e)
    {
        ActiveVm.ResetAllColumnWidths();
    }

    private void ResetActiveWidth_Click(object sender, RoutedEventArgs e)
    {
        ActiveVm.ResetActiveColumnWidth();
    }

    private void LockActiveWidth_Click(object sender, RoutedEventArgs e)
    {
        ActiveVm.ToggleLockActiveWidth();
    }

    private void RemoveActiveWithConfirmation()
    {
        var active = ActiveVm.GetActive();
        if (active is null)
            return;

        if (HasEditedColumnData(active))
        {
            var message = BuildDeleteColumnMessage(active);
            if (!ConfirmDestructiveAction("Delete Column", message))
                return;
        }

        ActiveVm.RemoveActiveColumn();
    }

    private static string BuildDeleteColumnMessage(ColumnViewModel column)
    {
        var preview = BuildColumnPreview(column.Text);
        if (!string.IsNullOrWhiteSpace(preview))
        {
            return $"Delete selected column \"{column.Title}\"?\n\nStarts with: \"{preview}\"\n\nThis permanently removes everything in that column.";
        }

        return $"Delete selected column \"{column.Title}\"?\n\nThis permanently removes everything in that column.";
    }

    private static string? BuildColumnPreview(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var firstLine = text
            .Replace("\r", string.Empty)
            .Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));

        if (string.IsNullOrWhiteSpace(firstLine))
            return null;

        return firstLine.Length <= 64 ? firstLine : firstLine[..61] + "...";
    }

    private static bool HasEditedColumnData(ColumnViewModel column)
    {
        if (!string.IsNullOrWhiteSpace(column.Text))
            return true;

        if (column.Images.Count > 0)
            return true;

        if (column.WidthPx.HasValue)
            return true;

        if (column.IsWidthLocked)
            return true;

        if (column.PastePreset != PasteListPreset.None)
            return true;

        if (!column.UseDefaultFont)
            return true;

        return false;
    }

    private void ResizeActiveColumn()
    {
        var active = ActiveVm.GetActive();
        if (active is null)
            return;

        var current = active.WidthPx ?? (int)DefaultColumnWidthPx;
        var prompt = PromptDialog.Show(this, "Resize Column", "Width (px):", current.ToString(CultureInfo.InvariantCulture));
        if (string.IsNullOrWhiteSpace(prompt))
            return;

        if (!int.TryParse(prompt, NumberStyles.Integer, CultureInfo.InvariantCulture, out var widthPx))
        {
            ActiveVm.StatusText = "Invalid width value.";
            return;
        }

        ActiveVm.SetActiveColumnWidth(widthPx);
    }

    private void SetActiveColumnFontFamily()
    {
        var active = ActiveVm.GetActive();
        if (active is null)
            return;

        var prompt = PromptDialog.ShowChoice(
            this,
            "Column Font Family",
            "Font family:",
            active.EditorFontFamily,
            ActiveVm.EditorFontFamilies);
        if (string.IsNullOrWhiteSpace(prompt))
            return;

        active.EditorFontFamily = prompt.Trim();
        active.UseDefaultFont = false;
        ActiveVm.RefreshStatus();
    }

    private void AdjustActiveColumnFontSize(double delta)
    {
        var active = ActiveVm.GetActive();
        if (active is null)
            return;

        active.EditorFontSize = Math.Clamp(active.EditorFontSize + delta, 8.0, 40.0);
        active.UseDefaultFont = false;
        ActiveVm.RefreshStatus();
    }

    private void ToggleActiveColumnBold()
    {
        var active = ActiveVm.GetActive();
        if (active is null)
            return;

        active.EditorFontWeight = active.EditorFontWeight == FontWeights.Bold
            ? FontWeights.Normal
            : FontWeights.Bold;
        active.UseDefaultFont = false;
        ActiveVm.RefreshStatus();
    }

    private void ToggleActiveColumnItalic()
    {
        var active = ActiveVm.GetActive();
        if (active is null)
            return;

        active.EditorFontStyle = active.EditorFontStyle == FontStyles.Italic
            ? FontStyles.Normal
            : FontStyles.Italic;
        active.UseDefaultFont = false;
        ActiveVm.RefreshStatus();
    }

    private void ResetActiveColumnFont()
    {
        var active = ActiveVm.GetActive();
        if (active is null)
            return;

        active.EditorFontFamily = ActiveVm.EditorFontFamily;
        active.EditorFontSize = ActiveVm.EditorFontSize;
        active.EditorFontStyle = ActiveVm.DefaultEditorFontStyle;
        active.EditorFontWeight = ActiveVm.DefaultEditorFontWeight;
        active.UseDefaultFont = true;
        ActiveVm.RefreshStatus();
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmWorkspaceDestructiveAction(ActiveWorkspace, "Clear All Columns", "Clearing all columns"))
            return;

        ActiveVm.ClearAll();
    }

    private void DuplicateActive_Click(object sender, RoutedEventArgs e)
    {
        ActiveVm.DuplicateActive();
    }

    private void ShowGutterBullets_Click(object sender, RoutedEventArgs e)
    {
        GetActiveEditorControl()?.ShowGutterBullets();
    }

    private void ShowGutterChecklist_Click(object sender, RoutedEventArgs e)
    {
        GetActiveEditorControl()?.ShowGutterChecklist();
    }

    private void SelectionToggleChecks_Click(object sender, RoutedEventArgs e)
    {
        GetActiveEditorControl()?.ToggleChecklistChecksInSelection();
    }

    private void ClearSelection_Click(object sender, RoutedEventArgs e)
    {
        ClearSelectionAndRefocusEditor();
    }

    private bool ClearSelectionAndRefocusEditor()
    {
        var editor = GetActiveEditorControl();
        if (editor is null)
            return false;

        var cleared = editor.ClearSelection();
        editor.FocusEditor();
        if (cleared)
            ActiveVm.StatusText = "Selection cleared.";
        return cleared;
    }

    private void SettingsComboBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || Keyboard.Modifiers != ModifierKeys.None)
            return;

        if (sender is not ComboBox comboBox)
            return;

        comboBox.IsDropDownOpen = false;
        e.Handled = true;

        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => ClearSelectionAndRefocusEditor()));
    }
}
