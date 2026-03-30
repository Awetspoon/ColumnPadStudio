using System.Windows;
using ColumnPadStudio.Domain.Enums;
using ColumnPadStudio.Domain.Logic;

namespace ColumnPadStudio.App.ViewModels;

public sealed partial class ShellViewModel
{
    private void SelectionToBullets()
    {
        if (CurrentColumn is null)
        {
            return;
        }

        var changed = CurrentColumn.ApplyPresetToSelection(PastePreset.Bullets);
        StatusText = changed ? "Applied bullets to selection." : "Selection already matches bullets.";
    }

    private void SelectionToChecklist()
    {
        if (CurrentColumn is null)
        {
            return;
        }

        var changed = CurrentColumn.ApplyPresetToSelection(PastePreset.Checklist);
        StatusText = changed ? "Applied checklist to selection." : "Selection already matches checklist.";
    }

    private void ClearAllColumns()
    {
        if (SelectedWorkspace is null)
        {
            return;
        }

        SelectedWorkspace.ClearAllColumns();
        StatusText = "Cleared all column text.";
    }

    private void DuplicateSelectedColumn()
    {
        if (SelectedWorkspace is null)
        {
            return;
        }

        var duplicated = SelectedWorkspace.DuplicateSelectedColumn();
        if (duplicated is not null)
        {
            StatusText = $"Duplicated {duplicated.Title}.";
            return;
        }

        StatusText = CurrentColumn is null
            ? "No column selected."
            : $"Maximum of {WorkspaceRules.MaxColumns} columns reached.";
    }

    private void UseSingleTextMode()
    {
        if (SelectedWorkspace is null)
        {
            return;
        }

        if (SelectedWorkspace.Columns.Count <= 1)
        {
            StatusText = "Already in single text mode.";
            return;
        }

        var removeCount = SelectedWorkspace.Columns.Count - 1;
        var result = MessageBox.Show($"Single text mode will remove {removeCount} other column(s). Continue?", "Single Text Mode", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        if (SelectedWorkspace.UseSingleTextMode())
        {
            StatusText = "Single text mode enabled.";
        }
    }

    private void UseColumnMode()
    {
        if (SelectedWorkspace is null)
        {
            return;
        }

        if (SelectedWorkspace.Columns.Count > 1)
        {
            StatusText = "Already in column mode.";
            return;
        }

        var count = SelectedWorkspace.UseColumnMode();
        StatusText = $"Column mode restored ({count} columns).";
    }

    private void SetTheme(ThemePreset preset)
    {
        if (SelectedWorkspace is null)
        {
            return;
        }

        SelectedWorkspace.ThemePreset = preset;
        ApplyCurrentWorkspaceTheme();
        StatusText = $"Theme set to {preset}.";
    }

    private void ResetSelectedWidth()
    {
        if (SelectedWorkspace is null)
        {
            return;
        }

        SelectedWorkspace.ResetSelectedWidth();
        StatusText = "Selected column width reset.";
    }

    private void ResetAllWidths()
    {
        if (SelectedWorkspace is null)
        {
            return;
        }

        SelectedWorkspace.ResetAllWidths();
        StatusText = "All column widths reset.";
    }

    private void ToggleSelectedWidthLock()
    {
        if (SelectedWorkspace?.SelectedColumn is null)
        {
            return;
        }

        var title = SelectedWorkspace.SelectedColumn.Title;
        var locked = SelectedWorkspace.ToggleSelectedWidthLock();
        StatusText = locked ? $"Froze {title} width." : $"{title} width can resize again.";
    }

    private void OpenFindDialog()
    {
        if (SelectedWorkspace is null)
        {
            return;
        }

        var window = new Views.FindReplaceWindow(_lastFindText, _lastReplaceText, showReplaceField: false)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        if (window.ShowDialog() != true)
        {
            StatusText = "Find cancelled.";
            return;
        }

        _lastFindText = window.SearchText.Trim();
        if (string.IsNullOrWhiteSpace(_lastFindText))
        {
            StatusText = "Enter text to find.";
            return;
        }

        _lastFindColumnIndex = SelectedWorkspace.SelectedColumn is null
            ? 0
            : Math.Max(0, SelectedWorkspace.Columns.IndexOf(SelectedWorkspace.SelectedColumn));
        _lastFindCharIndex = -1;
        FindNextInternal();
    }

    private void FindNext()
    {
        if (SelectedWorkspace is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_lastFindText))
        {
            OpenFindDialog();
            return;
        }

        FindNextInternal();
    }

    private void OpenReplaceAllDialog()
    {
        if (SelectedWorkspace is null)
        {
            return;
        }

        var window = new Views.FindReplaceWindow(_lastFindText, _lastReplaceText, showReplaceField: true)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        if (window.ShowDialog() != true)
        {
            StatusText = "Replace all cancelled.";
            return;
        }

        _lastFindText = window.SearchText.Trim();
        _lastReplaceText = window.ReplaceText;
        if (string.IsNullOrWhiteSpace(_lastFindText))
        {
            StatusText = "Enter text to replace.";
            return;
        }

        var totalMatches = 0;
        foreach (var column in SelectedWorkspace.Columns)
        {
            var replaceResult = WorkspaceSearchLogic.ReplaceAll(column.Text, _lastFindText, _lastReplaceText);
            if (replaceResult.Count == 0)
            {
                continue;
            }

            column.Text = replaceResult.Text;
            column.ClearRequestedSelection();
            totalMatches += replaceResult.Count;
        }

        if (totalMatches == 0)
        {
            StatusText = $"No match for '{_lastFindText}'.";
            return;
        }

        _lastFindCharIndex = -1;
        _lastFindColumnIndex = SelectedWorkspace.SelectedColumn is null
            ? 0
            : Math.Max(0, SelectedWorkspace.Columns.IndexOf(SelectedWorkspace.SelectedColumn));
        StatusText = $"Replaced {totalMatches} occurrence(s).";
    }

    private void FindNextInternal()
    {
        if (SelectedWorkspace is null || string.IsNullOrWhiteSpace(_lastFindText) || SelectedWorkspace.Columns.Count == 0)
        {
            return;
        }

        var columns = SelectedWorkspace.Columns;
        var startingColumnIndex = _lastFindCharIndex >= 0
            ? Math.Clamp(_lastFindColumnIndex, 0, columns.Count - 1)
            : SelectedWorkspace.SelectedColumn is null
                ? 0
                : Math.Max(0, columns.IndexOf(SelectedWorkspace.SelectedColumn));
        var startingCharIndex = _lastFindCharIndex >= 0
            ? _lastFindCharIndex + _lastFindText.Length
            : SelectedWorkspace.SelectedColumn?.SelectionEnd ?? 0;
        var hit = WorkspaceSearchLogic.FindNext(columns.Select(column => column.Text).ToList(), _lastFindText, startingColumnIndex, startingCharIndex);

        if (hit is null)
        {
            _lastFindCharIndex = -1;
            StatusText = $"No match for '{_lastFindText}'.";
            return;
        }

        for (var index = 0; index < columns.Count; index++)
        {
            if (index != hit.Value.ColumnIndex)
            {
                columns[index].ClearRequestedSelection();
            }
        }

        var matchedColumn = columns[hit.Value.ColumnIndex];
        SelectedWorkspace.SelectColumn(matchedColumn);
        _lastFindColumnIndex = hit.Value.ColumnIndex;
        _lastFindCharIndex = hit.Value.Start;
        matchedColumn.RequestSelection(hit.Value.Start, hit.Value.Length);
        StatusText = $"Found in {matchedColumn.Title} (line {hit.Value.LineNumber}).";
    }

    private void ToggleChecklistChecks()
    {
        if (CurrentColumn is null)
        {
            return;
        }

        var toggledCount = CurrentColumn.ToggleChecklistSelection();
        StatusText = toggledCount == 1
            ? "Toggled checklist checks on 1 line."
            : $"Toggled checklist checks on {toggledCount} lines.";
    }

    private void ClearSelection()
    {
        if (CurrentColumn is null)
        {
            return;
        }

        CurrentColumn.RequestSelection(CurrentColumn.SelectionStart, 0);
        StatusText = "Selection cleared.";
    }

    private void ToggleLineNumbers()
    {
        if (SelectedWorkspace is null)
        {
            return;
        }

        SelectedWorkspace.ShowLineNumbers = !SelectedWorkspace.ShowLineNumbers;
        StatusText = SelectedWorkspace.ShowLineNumbers ? "Line numbers on." : "Line numbers off.";
    }

    private void AddColumnToSelectedWorkspace()
    {
        if (SelectedWorkspace is null)
        {
            return;
        }

        if (SelectedWorkspace.Columns.Count >= WorkspaceRules.MaxColumns)
        {
            StatusText = $"Maximum of {WorkspaceRules.MaxColumns} columns reached.";
            return;
        }

        SelectedWorkspace.AddColumn();
        StatusText = $"Added {SelectedWorkspace.SelectedColumn?.Title ?? "column"}.";
    }

    private void RemoveSelectedColumn()
    {
        if (SelectedWorkspace?.SelectedColumn is null)
        {
            return;
        }

        if (SelectedWorkspace.Columns.Count <= 1)
        {
            StatusText = "The last remaining column cannot be removed.";
            return;
        }

        var title = SelectedWorkspace.SelectedColumn.Title;
        var needsWarning = SelectedWorkspace.SelectedColumn.HasMeaningfulEdits;
        if (!needsWarning || MessageBox.Show($"Delete {title}? This column has edits and will be removed immediately.", "Remove Column", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            SelectedWorkspace.RemoveSelectedColumn();
            StatusText = $"Removed {title}.";
        }
    }

    private void MoveSelectedColumnLeft()
    {
        if (SelectedWorkspace?.SelectedColumn is null)
        {
            return;
        }

        var title = SelectedWorkspace.SelectedColumn.Title;
        var index = SelectedWorkspace.Columns.IndexOf(SelectedWorkspace.SelectedColumn);
        if (index <= 0)
        {
            StatusText = $"{title} is already the first column.";
            return;
        }

        var swapWith = SelectedWorkspace.Columns[index - 1].Title;
        SelectedWorkspace.MoveSelectedColumnLeft();
        StatusText = $"Swapped {title} with {swapWith}.";
    }

    private void MoveSelectedColumnRight()
    {
        if (SelectedWorkspace?.SelectedColumn is null)
        {
            return;
        }

        var title = SelectedWorkspace.SelectedColumn.Title;
        var index = SelectedWorkspace.Columns.IndexOf(SelectedWorkspace.SelectedColumn);
        if (index >= SelectedWorkspace.Columns.Count - 1)
        {
            StatusText = $"{title} is already the last column.";
            return;
        }

        var swapWith = SelectedWorkspace.Columns[index + 1].Title;
        SelectedWorkspace.MoveSelectedColumnRight();
        StatusText = $"Swapped {title} with {swapWith}.";
    }
}
