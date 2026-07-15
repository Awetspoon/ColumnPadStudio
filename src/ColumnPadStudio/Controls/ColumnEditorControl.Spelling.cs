using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ColumnPadStudio.Controls;

public partial class ColumnEditorControl
{
    private const string SpellCheckMenuTag = "SpellCheckDynamicMenuItem";

    private void EditorContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        UpdateSpellCheckMenuItems();
        UpdatePastePresetMenuChecks();
    }

    private void Editor_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var characterIndex = Editor.GetCharacterIndexFromPoint(e.GetPosition(Editor), snapToText: true);
        if (characterIndex < 0)
            return;

        _editorContextMenuCharacterIndex = Math.Clamp(characterIndex, 0, Editor.Text.Length);
        if (!IsCharacterIndexInsideSelection(_editorContextMenuCharacterIndex))
            Editor.CaretIndex = _editorContextMenuCharacterIndex;
    }

    private bool IsCharacterIndexInsideSelection(int characterIndex)
    {
        if (Editor.SelectionLength <= 0)
            return false;

        var selectionStart = Editor.SelectionStart;
        var selectionEnd = selectionStart + Editor.SelectionLength;
        return characterIndex >= selectionStart && characterIndex < selectionEnd;
    }

    private void UpdateSpellCheckMenuItems()
    {
        if (Editor.ContextMenu is not { } contextMenu)
            return;

        RemoveSpellCheckMenuItems(contextMenu);
        if (!Editor.SpellCheck.IsEnabled)
            return;

        var spellingError = GetContextSpellingError();
        if (spellingError is null)
            return;

        var insertIndex = 0;
        var suggestionCount = 0;
        foreach (var suggestion in spellingError.Suggestions)
        {
            contextMenu.Items.Insert(insertIndex++, new MenuItem
            {
                Header = suggestion,
                FontWeight = FontWeights.Bold,
                Command = EditingCommands.CorrectSpellingError,
                CommandParameter = suggestion,
                CommandTarget = Editor,
                Tag = SpellCheckMenuTag
            });
            suggestionCount++;
        }

        if (suggestionCount == 0)
        {
            contextMenu.Items.Insert(insertIndex++, new MenuItem
            {
                Header = "No spelling suggestions",
                IsEnabled = false,
                Tag = SpellCheckMenuTag
            });
        }

        contextMenu.Items.Insert(insertIndex++, new Separator { Tag = SpellCheckMenuTag });
        contextMenu.Items.Insert(insertIndex++, new MenuItem
        {
            Header = "Ignore All",
            Command = EditingCommands.IgnoreSpellingError,
            CommandTarget = Editor,
            Tag = SpellCheckMenuTag
        });
        contextMenu.Items.Insert(insertIndex, new Separator { Tag = SpellCheckMenuTag });
    }

    private SpellingError? GetContextSpellingError()
    {
        var textLength = Editor.Text.Length;
        if (textLength == 0)
            return null;

        var characterIndex = _editorContextMenuCharacterIndex >= 0
            ? _editorContextMenuCharacterIndex
            : Editor.CaretIndex;

        characterIndex = Math.Clamp(characterIndex, 0, textLength - 1);
        var spellingError = Editor.GetSpellingError(characterIndex);
        if (spellingError is not null || characterIndex == 0)
            return spellingError;

        return Editor.GetSpellingError(characterIndex - 1);
    }

    private static void RemoveSpellCheckMenuItems(ContextMenu contextMenu)
    {
        for (var index = contextMenu.Items.Count - 1; index >= 0; index--)
        {
            if (contextMenu.Items[index] is FrameworkElement { Tag: string tag } &&
                string.Equals(tag, SpellCheckMenuTag, StringComparison.Ordinal))
            {
                contextMenu.Items.RemoveAt(index);
            }
        }
    }
}
