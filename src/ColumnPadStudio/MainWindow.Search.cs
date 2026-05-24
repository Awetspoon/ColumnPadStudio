using ColumnPadStudio.Controls;
using ColumnPadStudio.Services;
using System.Windows;

namespace ColumnPadStudio;

public partial class MainWindow
{
    private void Find_Click(object sender, RoutedEventArgs e)
    {
        var value = PromptDialog.Show(this, "Find", "Find text:", _lastFindText);
        if (string.IsNullOrWhiteSpace(value))
            return;

        _lastFindText = value;
        ResetFindCursor();
        FindNextCore();
    }

    private void FindNext_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastFindText))
        {
            Find_Click(sender, e);
            return;
        }

        FindNextCore();
    }

    private void ReplaceAll_Click(object sender, RoutedEventArgs e)
    {
        var find = PromptDialog.Show(this, "Replace All", "Find text:", _lastFindText);
        if (string.IsNullOrWhiteSpace(find))
            return;

        var replacement = PromptDialog.Show(this, "Replace All", "Replace with:", _lastReplaceText);
        if (replacement is null)
            return;

        _lastFindText = find;
        _lastReplaceText = replacement;

        var totalReplacements = 0;
        foreach (var column in ActiveVm.Columns)
        {
            var (replacedText, count) = TextSearchService.ReplaceAllWithCount(column.Text ?? string.Empty, find, replacement, StringComparison.CurrentCultureIgnoreCase);
            if (count <= 0)
                continue;

            column.Text = replacedText;
            totalReplacements += count;
        }

        ActiveVm.RefreshStatus();
        ActiveVm.StatusText = totalReplacements > 0
            ? $"Replaced {totalReplacements} occurrence(s)."
            : $"No match for '{find}'.";
    }

    private void ResetFindCursor()
    {
        _lastFoundColumnIndex = -1;
        _lastFoundCharIndex = -1;
    }

    private void FindNextCore()
    {
        if (string.IsNullOrWhiteSpace(_lastFindText))
            return;

        var vm = ActiveVm;
        if (vm.Columns.Count == 0)
            return;

        var active = vm.GetActive();
        var activeColumnIndex = active is null ? 0 : vm.Columns.IndexOf(active);
        if (activeColumnIndex < 0)
            activeColumnIndex = 0;

        var activeSelectionStart = 0;
        var activeSelectionLength = 0;
        if (active is not null && _editorsById.TryGetValue(active.Id, out var activeEditor))
        {
            activeSelectionStart = activeEditor.SelectionStart;
            activeSelectionLength = activeEditor.SelectionLength;
        }

        var columnTexts = vm.Columns.Select(column => column.Text).ToList();
        var cursor = new SearchCursor(_lastFoundColumnIndex, _lastFoundCharIndex);
        if (TextSearchService.TryFindNext(
                columnTexts,
                _lastFindText,
                activeColumnIndex,
                activeSelectionStart,
                activeSelectionLength,
                cursor,
                out var hit,
                StringComparison.CurrentCultureIgnoreCase))
        {
            FocusFindHit(hit.ColumnIndex, hit.CharIndex, hit.LineNumber);
            return;
        }

        vm.StatusText = $"No match for '{_lastFindText}'.";
    }

    private void FocusFindHit(int columnIndex, int hitIndex, int lineNumber)
    {
        var vm = ActiveVm;
        if (columnIndex < 0 || columnIndex >= vm.Columns.Count)
            return;

        var column = vm.Columns[columnIndex];
        vm.ActiveColumnId = column.Id;
        vm.RefreshStatus();

        if (!_editorsById.TryGetValue(column.Id, out var editor))
        {
            RebuildColumns();
            _editorsById.TryGetValue(column.Id, out editor);
        }

        editor?.FocusAndSelectRange(hitIndex, _lastFindText.Length);
        _lastFoundColumnIndex = columnIndex;
        _lastFoundCharIndex = hitIndex;
        vm.StatusText = $"Found in {column.Title} (line {lineNumber}).";
    }
}
