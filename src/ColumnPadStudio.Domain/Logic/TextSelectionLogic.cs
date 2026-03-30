namespace ColumnPadStudio.Domain.Logic;

public static class TextSelectionLogic
{
    public static (int StartLineIndex, int EndLineIndex) GetSelectedLineRange(string? text, int selectionStart, int selectionLength)
    {
        var source = text ?? string.Empty;
        if (source.Length == 0)
        {
            return (0, 0);
        }

        var start = Math.Clamp(selectionStart, 0, source.Length);
        var endExclusive = Math.Clamp(start + Math.Max(0, selectionLength), 0, source.Length);

        var startLineIndex = GetLineIndexForPosition(source, start);
        var endLinePosition = endExclusive;

        if (selectionLength > 0 && endExclusive > start && IsAtLineStart(source, endExclusive))
        {
            endLinePosition--;
        }

        var endLineIndex = GetLineIndexForPosition(source, endLinePosition);
        return (Math.Min(startLineIndex, endLineIndex), Math.Max(startLineIndex, endLineIndex));
    }

    private static int GetLineIndexForPosition(string text, int position)
    {
        var boundedPosition = Math.Clamp(position, 0, text.Length);
        var lineIndex = 0;
        for (var i = 0; i < boundedPosition; i++)
        {
            if (text[i] == '\n')
            {
                lineIndex++;
            }
        }

        return lineIndex;
    }

    private static bool IsAtLineStart(string text, int position)
    {
        if (position <= 0 || position > text.Length)
        {
            return false;
        }

        return text[position - 1] == '\n';
    }
}
