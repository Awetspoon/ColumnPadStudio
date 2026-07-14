using ColumnPadStudio.Controls;
using ColumnPadStudio.Services;
using System.Windows;

namespace ColumnPadStudio;

public partial class MainWindow
{
    private void ThemeLight_Click(object sender, RoutedEventArgs e) => SetTheme(ThemePresetService.LightPreset);
    private void ThemeDark_Click(object sender, RoutedEventArgs e) => SetTheme(ThemePresetService.DarkPreset);
    private void ThemeDefault_Click(object sender, RoutedEventArgs e) => SetTheme(ThemePresetService.DefaultPreset);

    private void ResetEditorFont_Click(object sender, RoutedEventArgs e)
    {
        ActiveVm.EditorFontFamily = "Consolas";
        ActiveVm.EditorFontStyleName = "Regular";
        ActiveVm.EditorFontSize = 13;
    }

    private void SetTheme(string preset)
    {
        ActiveVm.ThemePreset = preset;
    }

    private void SingleTextMode_Click(object sender, RoutedEventArgs e)
    {
        var vm = ActiveVm;
        if (vm.Columns.Count <= 1)
        {
            vm.StatusText = "Already in single text mode.";
            return;
        }

        var selected = vm.GetActive();
        if (selected is null)
            return;

        var removedCount = vm.Columns.Count - 1;
        var removedLabel = removedCount == 1 ? "column" : "columns";
        var prompt = $"Single Text Mode keeps only \"{selected.Title}\" in this workspace and removes {removedCount} other {removedLabel}.\n\nContinue?";
        if (!ConfirmDestructiveAction("Single Text Mode", prompt))
            return;

        if (ActiveWorkspace is { } workspace)
            workspace.LastMultiColumnCount = Math.Max(2, vm.Columns.Count);

        if (!vm.KeepOnlyColumn(selected.Id))
            return;

        vm.StatusText = "Single text mode enabled.";
    }

    private void ColumnMode_Click(object sender, RoutedEventArgs e)
    {
        var vm = ActiveVm;
        if (vm.Columns.Count > 1)
        {
            vm.StatusText = "Already in column mode.";
            return;
        }

        var targetColumns = Math.Max(2, ActiveWorkspace?.LastMultiColumnCount ?? 3);
        vm.SetColumnCount(targetColumns);
        RebuildColumns();
        vm.RefreshStatus();
        vm.StatusText = $"Column mode restored ({targetColumns} columns).";
    }

    private void OpenWorkflowBuilder_Click(object sender, RoutedEventArgs e)
    {
        OpenWorkflowBuilder();
    }

    private void OpenWorkflowBuilder(string? importFilePath = null)
    {
        if (_workflowBuilderWindow is not null)
        {
            if (_workflowBuilderWindow.WindowState == WindowState.Minimized)
                _workflowBuilderWindow.WindowState = WindowState.Normal;

            _workflowBuilderWindow.Activate();
            _workflowBuilderWindow.Focus();
            if (!string.IsNullOrWhiteSpace(importFilePath))
                _workflowBuilderWindow.ImportWorkflowJsonFromPath(importFilePath);
            return;
        }

        var window = new WorkflowBuilderWindow(importFilePath);

        window.Closed += (_, __) => _workflowBuilderWindow = null;
        _workflowBuilderWindow = window;

        window.Show();
    }

    private void ApplyTheme(string preset)
    {
        ThemeResourceService.ApplyTheme(Application.Current.Resources, preset);
    }
}
