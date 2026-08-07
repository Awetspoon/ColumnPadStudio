using ColumnPadStudio.Domain.Lists;
using ColumnPadStudio.Services;
using ColumnPadStudio.ViewModels;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColumnPadStudio.Controls;

public partial class ColumnEditorControl
{
    private void HeaderGrip_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        ActivateColumnForActions();
    }

    private void ColumnActionsButton_Click(object sender, RoutedEventArgs e)
    {
        ActivateColumnForActions();

        if (ColumnActionsButton.ContextMenu is not { } columnContextMenu)
            return;

        columnContextMenu.PlacementTarget = ColumnActionsButton;
        columnContextMenu.IsOpen = true;
    }

    private void ActivateColumnForActions()
    {
        ColumnActionsOpening?.Invoke(this, EventArgs.Empty);
    }

    private void ColumnContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (VM is null)
            return;

        ColumnFontBoldMenuItem.IsChecked = VM.EditorFontWeight == FontWeights.Bold;
        ColumnFontItalicMenuItem.IsChecked = VM.EditorFontStyle == FontStyles.Italic;
        RefreshTextColorMenuChecks();
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

    private void ColumnTextColorPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string value })
            SetTextColorRequested?.Invoke(this, new ColumnTextColorEventArgs(value));
    }

    private void ColumnTextColorCustom_Click(object sender, RoutedEventArgs e)
    {
        SetCustomTextColorRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshTextColorMenuChecks()
    {
        if (VM is null)
            return;

        foreach (var item in ColumnTextColorMenuItem.Items.OfType<MenuItem>())
        {
            if (item.Tag is string value)
                item.IsChecked = string.Equals(value, VM.EditorTextColor, StringComparison.Ordinal);
        }

        var isCustom = ColumnTextColorService.IsCustom(VM.EditorTextColor);
        ColumnTextColorCustomMenuItem.IsChecked = isCustom;
        ColumnTextColorCustomSwatch.Background = VM.CustomEditorTextColorBrush ?? Brushes.Transparent;
        ColumnTextColorCustomLabel.Text = isCustom
            ? $"Custom... ({VM.EditorTextColor})"
            : "Custom...";
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

        var (startLine, endLine) = GetSelectedLogicalLineRange();
        for (var i = startLine; i <= endLine; i++)
            VM.ToggleChecklistLineChecked(i);

        QueueLineNumberRefresh();
    }

    private (int StartLine, int EndLine) GetSelectedLogicalLineRange()
    {
        var selectionStart = Editor.SelectionStart;
        var selectionEnd = selectionStart + Editor.SelectionLength;
        var editorText = Editor.Text ?? string.Empty;

        var startLine = GetLogicalLineIndexFromCharacterIndex(selectionStart);
        var endLine = GetLogicalLineIndexFromCharacterIndex(selectionEnd);

        if (selectionEnd > selectionStart &&
            endLine > startLine &&
            IsLogicalLineStart(editorText, selectionEnd))
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
