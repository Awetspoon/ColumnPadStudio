namespace ColumnPadStudio.Services;

public readonly record struct SearchCursor(int ColumnIndex, int CharIndex)
{
    public static SearchCursor Empty => new(-1, -1);
}

public readonly record struct SearchResult(int ColumnIndex, int CharIndex, int LineNumber);

public static class TextSearchService
{
    public static bool TryFindNext(
        IReadOnlyList<string?> columnTexts,
        string? findText,
        int activeColumnIndex,
        int activeSelectionStart,
        int activeSelectionLength,
        SearchCursor lastCursor,
        out SearchResult result,
        StringComparison comparison = StringComparison.CurrentCultureIgnoreCase)
    {
        result = default;

        if (columnTexts is null)
            throw new ArgumentNullException(nameof(columnTexts));

        if (columnTexts.Count == 0 || string.IsNullOrWhiteSpace(findText))
            return false;

        var startColumnIndex = 0;
        var startCharIndex = 0;

        if (lastCursor.ColumnIndex >= 0 && lastCursor.ColumnIndex < columnTexts.Count)
        {
            startColumnIndex = lastCursor.ColumnIndex;
            startCharIndex = lastCursor.CharIndex + findText.Length;
        }
        else
        {
            startColumnIndex = activeColumnIndex;
            if (startColumnIndex < 0 || startColumnIndex >= columnTexts.Count)
                startColumnIndex = 0;

            var activeText = columnTexts[startColumnIndex] ?? string.Empty;
            startCharIndex = Math.Clamp(activeSelectionStart + activeSelectionLength, 0, activeText.Length);
        }

        for (var offset = 0; offset < columnTexts.Count; offset++)
        {
            var index = (startColumnIndex + offset) % columnTexts.Count;
            var text = columnTexts[index] ?? string.Empty;
            var from = offset == 0 ? Math.Clamp(startCharIndex, 0, text.Length) : 0;
            var hit = text.IndexOf(findText, from, comparison);

            if (hit >= 0)
            {
                result = new SearchResult(index, hit, ComputeLineNumber(text, hit));
                return true;
            }
        }

        if (startCharIndex > 0)
        {
            var firstText = columnTexts[startColumnIndex] ?? string.Empty;
            var wrapLimit = Math.Min(startCharIndex, firstText.Length);
            var wrapText = firstText[..wrapLimit];
            var wrapHit = wrapText.IndexOf(findText, comparison);

            if (wrapHit >= 0)
            {
                result = new SearchResult(startColumnIndex, wrapHit, ComputeLineNumber(firstText, wrapHit));
                return true;
            }
        }

        return false;
    }

    public static int ComputeLineNumber(string? text, int charIndex)
    {
        if (string.IsNullOrEmpty(text) || charIndex <= 0)
            return 1;

        var limit = Math.Min(charIndex, text.Length);
        var line = 1;
        for (var i = 0; i < limit; i++)
        {
            if (text[i] == '\n')
                line++;
        }

        return line;
    }

    public static (string Replaced, int Count) ReplaceAllWithCount(
        string? source,
        string? find,
        string? replacement,
        StringComparison comparison = StringComparison.CurrentCultureIgnoreCase)
    {
        var safeSource = source ?? string.Empty;
        var safeFind = find ?? string.Empty;
        var safeReplacement = replacement ?? string.Empty;

        if (safeSource.Length == 0 || safeFind.Length == 0)
            return (safeSource, 0);

        var index = 0;
        var count = 0;
        var result = new System.Text.StringBuilder(safeSource.Length);

        while (index < safeSource.Length)
        {
            var hit = safeSource.IndexOf(safeFind, index, comparison);
            if (hit < 0)
            {
                result.Append(safeSource, index, safeSource.Length - index);
                break;
            }

            result.Append(safeSource, index, hit - index);
            result.Append(safeReplacement);
            index = hit + safeFind.Length;
            count++;
        }

        return count == 0 ? (safeSource, 0) : (result.ToString(), count);
    }
}
