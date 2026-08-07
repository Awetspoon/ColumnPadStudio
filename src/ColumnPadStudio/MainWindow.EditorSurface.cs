using ColumnPadStudio.Controls;
using ColumnPadStudio.Domain.Workspaces;
using ColumnPadStudio.Domain.Lists;
using ColumnPadStudio.Models;
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
        if (!CanManageColumnWidths)
        {
            ActiveVm.StatusText = GetWidthManagementUnavailableStatus(ActiveVm, "resetting column widths");
            return;
        }

        ResetAllColumnsToDefault(ActiveVm);
    }

    private void ResetActiveWidth_Click(object sender, RoutedEventArgs e)
    {
        if (!CanManageColumnWidths)
        {
            ActiveVm.StatusText = GetWidthManagementUnavailableStatus(ActiveVm, "resetting a column width");
            return;
        }

        ResetSelectedColumnToDefault(ActiveVm);
    }

    private void LockActiveWidth_Click(object sender, RoutedEventArgs e)
    {
        var vm = ActiveVm;
        var active = vm.GetActive();
        if (active is null)
            return;

        if (!CanManageColumnWidths)
        {
            vm.StatusText = GetWidthManagementUnavailableStatus(vm, "freezing a column width");
            return;
        }

        if (_editorsById.TryGetValue(active.Id, out var editor))
            ToggleColumnWidthLock(editor, vm, active);
        else
        {
            if (!active.IsWidthLocked && !active.WidthPx.HasValue)
                active.WidthPx = _appPreferences.DefaultColumnWidthPx;
            vm.ToggleLockActiveWidth();
        }
    }

    private void UseStandardColumnWidth_Click(object sender, RoutedEventArgs e)
    {
        UpdateDefaultColumnWidth((int)WorkspaceConstraints.DefaultColumnWidth);
    }

    private void SetDefaultColumnWidth_Click(object sender, RoutedEventArgs e)
    {
        var current = _appPreferences.DefaultColumnWidthPx.ToString(CultureInfo.InvariantCulture);
        var prompt = PromptDialog.Show(
            this,
            "Default Column Width",
            $"Default width ({WorkspaceConstraints.MinimumColumnWidth:0}-{WorkspaceConstraints.MaximumColumnWidth:0} px):",
            current);
        if (string.IsNullOrWhiteSpace(prompt))
        {
            RefreshColumnWidthPreferenceBindings();
            return;
        }

        if (!int.TryParse(prompt, NumberStyles.Integer, CultureInfo.InvariantCulture, out var widthPx)
            || widthPx < WorkspaceConstraints.MinimumColumnWidth
            || widthPx > WorkspaceConstraints.MaximumColumnWidth)
        {
            ActiveVm.StatusText = $"Default column width must be between {WorkspaceConstraints.MinimumColumnWidth:0} and {WorkspaceConstraints.MaximumColumnWidth:0}px.";
            RefreshColumnWidthPreferenceBindings();
            return;
        }

        UpdateDefaultColumnWidth(widthPx);
    }

    private void SetColumnSpacing_Click(object sender, RoutedEventArgs e)
    {
        var current = _appPreferences.ColumnSpacingPx.ToString(CultureInfo.InvariantCulture);
        var prompt = PromptDialog.Show(
            this,
            "Column Gap",
            $"Gap between snapped columns ({AppPreferences.MinimumColumnSpacingPx}-{AppPreferences.MaximumColumnSpacingPx} px):",
            current);
        if (string.IsNullOrWhiteSpace(prompt))
            return;

        if (!int.TryParse(prompt, NumberStyles.Integer, CultureInfo.InvariantCulture, out var spacingPx)
            || spacingPx < AppPreferences.MinimumColumnSpacingPx
            || spacingPx > AppPreferences.MaximumColumnSpacingPx)
        {
            ActiveVm.StatusText = $"Column gap must be between {AppPreferences.MinimumColumnSpacingPx} and {AppPreferences.MaximumColumnSpacingPx}px.";
            return;
        }

        UpdateColumnSpacing(spacingPx);
    }

    private void SetGutterWidth_Click(object sender, RoutedEventArgs e)
    {
        var vm = ActiveVm;
        var current = vm.GutterWidthPx.ToString(CultureInfo.InvariantCulture);
        var prompt = PromptDialog.Show(
            this,
            "Gutter Width",
            $"Gutter width ({MainViewModel.MinimumGutterWidthPx}-{MainViewModel.MaximumGutterWidthPx} px):",
            current);
        if (string.IsNullOrWhiteSpace(prompt))
            return;

        if (!int.TryParse(prompt, NumberStyles.Integer, CultureInfo.InvariantCulture, out var gutterWidthPx)
            || gutterWidthPx < MainViewModel.MinimumGutterWidthPx
            || gutterWidthPx > MainViewModel.MaximumGutterWidthPx)
        {
            vm.StatusText = $"Gutter width must be between {MainViewModel.MinimumGutterWidthPx} and {MainViewModel.MaximumGutterWidthPx}px.";
            return;
        }

        vm.GutterWidthPx = gutterWidthPx;
        if (!vm.ShowLineNumbers)
            vm.StatusText = $"Gutter width saved as {gutterWidthPx}px. Turn on line numbers to see it.";
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

        if (column.EditorTextColor != ColumnTextColorService.ThemeDefault)
            return true;

        return false;
    }

    private void ResizeActiveColumn()
    {
        var active = ActiveVm.GetActive();
        if (active is null)
            return;

        if (!CanManageColumnWidths)
        {
            ActiveVm.StatusText = GetWidthManagementUnavailableStatus(ActiveVm, "resizing an individual column");
            return;
        }

        var current = active.WidthPx ?? _appPreferences.DefaultColumnWidthPx;
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
