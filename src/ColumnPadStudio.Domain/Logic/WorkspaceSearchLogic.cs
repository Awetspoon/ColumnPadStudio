using System.Globalization;

namespace ColumnPadStudio.Domain.Logic;

public static class WorkspaceSearchLogic
{
    public static WorkspaceSearchHit? FindNext(IReadOnlyList<string> columnTexts, string searchText, int startingColumnIndex, int startingCharIndex)
    {
        if (columnTexts.Count == 0 || string.IsNullOrEmpty(searchText))
        {
            return null;
        }

        var boundedColumnIndex = Math.Clamp(startingColumnIndex, 0, columnTexts.Count - 1);

        for (var offset = 0; offset < columnTexts.Count; offset++)
        {
            var columnIndex = (boundedColumnIndex + offset) % columnTexts.Count;
            var text = columnTexts[columnIndex] ?? string.Empty;
            var startIndex = offset == 0 ? Math.Clamp(startingCharIndex, 0, text.Length) : 0;
            var foundIndex = IndexOf(text, searchText, startIndex, text.Length - startIndex);
            if (foundIndex >= 0)
            {
                return new WorkspaceSearchHit(columnIndex, foundIndex, searchText.Length, GetLineNumber(text, foundIndex));
            }
        }

        var startColumnText = columnTexts[boundedColumnIndex] ?? string.Empty;
        var wrapCount = Math.Clamp(startingCharIndex, 0, startColumnText.Length);
        if (wrapCount <= 0)
        {
            return null;
        }

        var wrappedIndex = IndexOf(startColumnText, searchText, 0, wrapCount);
        return wrappedIndex >= 0
            ? new WorkspaceSearchHit(boundedColumnIndex, wrappedIndex, searchText.Length, GetLineNumber(startColumnText, wrappedIndex))
            : null;
    }

    public static (string Text, int Count) ReplaceAll(string text, string searchText, string replaceText)
    {
        var source = text ?? string.Empty;
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(searchText))
        {
            return (source, 0);
        }

        var count = 0;
        var startIndex = 0;
        var builder = new System.Text.StringBuilder();

        while (startIndex < source.Length)
        {
            var foundIndex = IndexOf(source, searchText, startIndex, source.Length - startIndex);
            if (foundIndex < 0)
            {
                builder.Append(source, startIndex, source.Length - startIndex);
                break;
            }

            builder.Append(source, startIndex, foundIndex - startIndex);
            builder.Append(replaceText);
            startIndex = foundIndex + searchText.Length;
            count++;
        }

        return count == 0 ? (source, 0) : (builder.ToString(), count);
    }

    public static int GetLineNumber(string text, int charIndex)
    {
        var source = text ?? string.Empty;
        if (source.Length == 0)
        {
            return 1;
        }

        var boundedIndex = Math.Clamp(charIndex, 0, source.Length);
        var lineNumber = 1;
        for (var i = 0; i < boundedIndex; i++)
        {
            if (source[i] == '\n')
            {
                lineNumber++;
            }
        }

        return lineNumber;
    }

    private static int IndexOf(string text, string searchText, int startIndex, int count)
    {
        if (count <= 0)
        {
            return -1;
        }

        return CultureInfo.CurrentCulture.CompareInfo.IndexOf(text, searchText, startIndex, count, CompareOptions.IgnoreCase);
    }
}

public readonly record struct WorkspaceSearchHit(int ColumnIndex, int Start, int Length, int LineNumber);
