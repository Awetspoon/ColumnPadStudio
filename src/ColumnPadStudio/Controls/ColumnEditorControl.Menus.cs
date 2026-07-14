using ColumnPadStudio.Domain.Lists;
using ColumnPadStudio.ViewModels;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ColumnPadStudio.Controls;

public partial class ColumnEditorControl
{
    private const string SpellCheckMenuTag = "SpellCheckDynamicMenuItem";

    private void ColumnContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        UpdatePastePresetMenuChecks();

        if (VM is null)
            return;

        ColumnFontBoldMenuItem.IsChecked = VM.EditorFontWeight == FontWeights.Bold;
        ColumnFontItalicMenuItem.IsChecked = VM.EditorFontStyle == FontStyles.Italic;
        RefreshPicturesMenu();
    }

    private void ClearSelection_Click(object sender, RoutedEventArgs e)
    {
        ClearSelection();
    }

    private void ColumnMenuRename_Click(object sender, RoutedEventArgs e)
    {
        if (VM is not null)
            VM.IsRenaming = true;
    }

    private void ColumnMenuDelete_Click(object sender, RoutedEventArgs e)
    {
        DeleteRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ColumnMenuMoveLeft_Click(object sender, RoutedEventArgs e)
    {
        MoveLeftRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ColumnMenuMoveRight_Click(object sender, RoutedEventArgs e)
    {
        MoveRightRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ColumnMenuResetWidth_Click(object sender, RoutedEventArgs e)
    {
        ResetWidthRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ColumnMenuResize_Click(object sender, RoutedEventArgs e)
    {
        ResizeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void InsertPicture_Click(object sender, RoutedEventArgs e)
    {
        InsertImageRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ImageRemove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ColumnImageViewModel image)
            RemoveImageRequested?.Invoke(this, new ColumnImageEventArgs(image));
    }

    private void RefreshPicturesMenu()
    {
        ColumnPicturesMenuItem.Items.Clear();
        ColumnPicturesMenuItem.Visibility = VM?.Images.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (VM is null)
            return;

        foreach (var image in VM.Images)
        {
            var item = new MenuItem
            {
                Header = image.DisplayName,
                Tag = image,
                IsCheckable = true,
                IsChecked = image.IsSelected
            };
            item.Click += ImageSelectFromMenu_Click;
            ColumnPicturesMenuItem.Items.Add(item);
        }
    }

    private void ColumnMenuToggleWidthLock_Click(object sender, RoutedEventArgs e)
    {
        LockWidthRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ColumnFontSetFamily_Click(object sender, RoutedEventArgs e)
    {
        SetFontFamilyRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ColumnFontIncrease_Click(object sender, RoutedEventArgs e)
    {
        IncreaseFontRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ColumnFontDecrease_Click(object sender, RoutedEventArgs e)
    {
        DecreaseFontRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ColumnFontBold_Click(object sender, RoutedEventArgs e)
    {
        ToggleBoldRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ColumnFontItalic_Click(object sender, RoutedEventArgs e)
    {
        ToggleItalicRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ColumnFontReset_Click(object sender, RoutedEventArgs e)
    {
        ResetFontRequested?.Invoke(this, EventArgs.Empty);
    }

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
        for (var i = contextMenu.Items.Count - 1; i >= 0; i--)
        {
            if (contextMenu.Items[i] is FrameworkElement { Tag: string tag } &&
                string.Equals(tag, SpellCheckMenuTag, StringComparison.Ordinal))
            {
                contextMenu.Items.RemoveAt(i);
            }
        }
    }

    private void PastePresetNone_Click(object sender, RoutedEventArgs e) => SetPastePreset(PasteListPreset.None);
    private void PastePresetBullets_Click(object sender, RoutedEventArgs e) => SetPastePreset(PasteListPreset.Bullets);
    private void PastePresetChecklist_Click(object sender, RoutedEventArgs e) => SetPastePreset(PasteListPreset.Checklist);

    private void ToggleBullets_Click(object sender, RoutedEventArgs e) => SetLineMarkerMode(LineMarkerMode.Bullets);
    private void ToggleChecklist_Click(object sender, RoutedEventArgs e) => SetLineMarkerMode(LineMarkerMode.Checklist);
    private void ToggleCheckMarks_Click(object sender, RoutedEventArgs e) => ToggleChecklistChecksForSelection();

    private void ToggleChecklistChecksForSelection()
    {
        if (VM is null)
            return;

        if (VM.LineMarkerMode != LineMarkerMode.Checklist)
            VM.LineMarkerMode = LineMarkerMode.Checklist;

        var (startLine, endLine) = GetSelectedLineRange();
        for (var i = startLine; i <= endLine; i++)
            VM.ToggleChecklistLineChecked(i);

        QueueLineNumberRefresh();
    }

    private (int StartLine, int EndLine) GetSelectedLineRange()
    {
        var selectionStart = Editor.SelectionStart;
        var selectionEnd = selectionStart + Editor.SelectionLength;

        var startLine = Editor.GetLineIndexFromCharacterIndex(selectionStart);
        var endLine = Editor.GetLineIndexFromCharacterIndex(selectionEnd);

        if (selectionEnd > selectionStart &&
            endLine > startLine &&
            selectionEnd == Editor.GetCharacterIndexFromLineIndex(endLine))
        {
            endLine--;
        }

        if (endLine < startLine)
            endLine = startLine;

        return (startLine, endLine);
    }

    private void SetPastePreset(PasteListPreset preset)
    {
        if (VM is null)
            return;

        VM.PastePreset = preset;
        UpdatePastePresetMenuChecks();
    }

    private void UpdatePastePresetMenuChecks()
    {
        if (Editor.ContextMenu is null)
            return;

        var presetMenu = Editor.ContextMenu.Items
            .OfType<MenuItem>()
            .FirstOrDefault(item => string.Equals(item.Tag as string, "PastePresetMenu", StringComparison.Ordinal));

        if (presetMenu is null)
            return;

        var activePreset = VM?.PastePreset ?? PasteListPreset.None;
        foreach (var child in presetMenu.Items.OfType<MenuItem>())
        {
            if (child.Tag is not string tag || !Enum.TryParse<PasteListPreset>(tag, ignoreCase: true, out var preset))
                continue;

            child.IsChecked = preset == activePreset;
        }
    }
}
