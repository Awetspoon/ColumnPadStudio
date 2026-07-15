using ColumnPadStudio.Domain.Lists;

namespace ColumnPadStudio.ViewModels;

public sealed partial class ColumnViewModel
{
    public IReadOnlyList<int> GetCheckedChecklistLineIndexes()
    {
        var sorted = _checkedChecklistLineIndexes.ToList();
        sorted.Sort();
        return sorted;
    }

    public void SetCheckedChecklistLineIndexes(IEnumerable<int>? lineIndexes)
    {
        var next = new HashSet<int>();
        if (lineIndexes is not null)
        {
            foreach (var lineIndex in lineIndexes)
            {
                if (lineIndex >= 0)
                    next.Add(lineIndex);
            }
        }

        if (_checkedChecklistLineIndexes.SetEquals(next))
            return;

        _checkedChecklistLineIndexes = next;
        TrimChecklistLineIndexesToBounds();
        RecomputeDerivedMetrics();
    }

    public bool IsChecklistLineChecked(int lineIndex)
        => lineIndex >= 0 && _checkedChecklistLineIndexes.Contains(lineIndex);

    public void ToggleChecklistLineChecked(int lineIndex)
    {
        if (lineIndex < 0)
            return;

        if (!_checkedChecklistLineIndexes.Remove(lineIndex))
            _checkedChecklistLineIndexes.Add(lineIndex);

        TrimChecklistLineIndexesToBounds();
        RecomputeDerivedMetrics();
    }

    private void RemapChecklistLineIndexes(string? previousText, string? nextText)
    {
        if (_checkedChecklistLineIndexes.Count == 0 || string.Equals(previousText, nextText, StringComparison.Ordinal))
            return;

        var oldLines = SplitLines(previousText);
        var newLines = SplitLines(nextText);
        if (newLines.Length == 1 && newLines[0].Length == 0)
        {
            _checkedChecklistLineIndexes.Clear();
            return;
        }

        var commonPrefix = 0;
        while (commonPrefix < oldLines.Length &&
               commonPrefix < newLines.Length &&
               string.Equals(oldLines[commonPrefix], newLines[commonPrefix], StringComparison.Ordinal))
        {
            commonPrefix++;
        }

        var commonSuffix = 0;
        while (commonSuffix < oldLines.Length - commonPrefix &&
               commonSuffix < newLines.Length - commonPrefix &&
               string.Equals(
                   oldLines[oldLines.Length - 1 - commonSuffix],
                   newLines[newLines.Length - 1 - commonSuffix],
                   StringComparison.Ordinal))
        {
            commonSuffix++;
        }

        var oldChangedLineCount = oldLines.Length - commonPrefix - commonSuffix;
        var newChangedLineCount = newLines.Length - commonPrefix - commonSuffix;
        var lineCountDelta = newLines.Length - oldLines.Length;
        var remapped = new HashSet<int>();

        foreach (var oldIndex in _checkedChecklistLineIndexes)
        {
            if (oldIndex < commonPrefix)
            {
                remapped.Add(oldIndex);
                continue;
            }

            if (oldIndex >= commonPrefix + oldChangedLineCount)
            {
                remapped.Add(oldIndex + lineCountDelta);
                continue;
            }

            if (newChangedLineCount > 0)
            {
                var relativeIndex = oldIndex - commonPrefix;
                remapped.Add(commonPrefix + Math.Min(relativeIndex, newChangedLineCount - 1));
            }
        }

        _checkedChecklistLineIndexes = remapped;
    }

    private void UpdateMetricsText()
    {
        var displayedLines = _visibleLineCount ?? LineCount;
        MetricsText = ChecklistTotal > 0
            ? $"{WordCount} words | {displayedLines} lines | {ChecklistDone}/{ChecklistTotal} done"
            : $"{WordCount} words | {displayedLines} lines";
    }

    private void RecomputeDerivedMetrics()
    {
        var text = _text ?? string.Empty;

        var lines = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n' || (text[i] == '\r' && (i + 1 >= text.Length || text[i + 1] != '\n')))
                lines++;
        }

        LineCount = lines;
        TrimChecklistLineIndexesToBounds();

        var wordCount = 0;
        var inWord = false;
        for (var i = 0; i < text.Length; i++)
        {
            var isWhite = char.IsWhiteSpace(text[i]);
            if (!isWhite && !inWord)
            {
                wordCount++;
                inWord = true;
            }
            else if (isWhite)
            {
                inWord = false;
            }
        }

        WordCount = wordCount;

        if (LineMarkerMode == LineMarkerMode.Checklist)
        {
            var splitLines = SplitLines(text);
            var checklistTotal = 0;
            var checklistDone = 0;

            for (var i = 0; i < splitLines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(splitLines[i]))
                    continue;

                checklistTotal++;
                if (_checkedChecklistLineIndexes.Contains(i))
                    checklistDone++;
            }

            ChecklistTotal = checklistTotal;
            ChecklistDone = checklistDone;
        }
        else
        {
            var checklistMetrics = ChecklistMetricsCalculator.Compute(text);
            ChecklistTotal = checklistMetrics.Total;
            ChecklistDone = checklistMetrics.Done;
        }

        UpdateMetricsText();
    }

    private void TrimChecklistLineIndexesToBounds()
    {
        var maxIndex = Math.Max(0, LineCount - 1);
        _checkedChecklistLineIndexes.RemoveWhere(index => index < 0 || index > maxIndex);
    }

    private static string[] SplitLines(string? text)
        => (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
}
