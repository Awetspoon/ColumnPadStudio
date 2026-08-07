using ColumnPadStudio.Controls;
using ColumnPadStudio.Services;
using System.Windows;

namespace ColumnPadStudio;

public partial class MainWindow
{
    private void SetActiveColumnFontFamily()
    {
        var active = ActiveVm.GetActive();
        if (active is null)
            return;

        var prompt = PromptDialog.ShowChoice(
            this,
            "Column Font Family",
            "Font family:",
            active.EditorFontFamily,
            ActiveVm.EditorFontFamilies);
        if (string.IsNullOrWhiteSpace(prompt))
            return;

        ActiveVm.PrepareForRichContent();
        active.EditorFontFamily = prompt.Trim();
        active.UseDefaultFont = false;
        ActiveVm.RefreshStatus();
    }

    private void AdjustActiveColumnFontSize(double delta)
    {
        var active = ActiveVm.GetActive();
        if (active is null)
            return;

        var nextSize = Math.Clamp(active.EditorFontSize + delta, 8.0, 40.0);
        if (Math.Abs(nextSize - active.EditorFontSize) < 0.001)
            return;

        ActiveVm.PrepareForRichContent();
        active.EditorFontSize = nextSize;
        active.UseDefaultFont = false;
        ActiveVm.RefreshStatus();
    }

    private void ToggleActiveColumnBold()
    {
        var active = ActiveVm.GetActive();
        if (active is null)
            return;

        ActiveVm.PrepareForRichContent();
        active.EditorFontWeight = active.EditorFontWeight == FontWeights.Bold
            ? FontWeights.Normal
            : FontWeights.Bold;
        active.UseDefaultFont = false;
        ActiveVm.RefreshStatus();
    }

    private void ToggleActiveColumnItalic()
    {
        var active = ActiveVm.GetActive();
        if (active is null)
            return;

        ActiveVm.PrepareForRichContent();
        active.EditorFontStyle = active.EditorFontStyle == FontStyles.Italic
            ? FontStyles.Normal
            : FontStyles.Italic;
        active.UseDefaultFont = false;
        ActiveVm.RefreshStatus();
    }

    private void ResetActiveColumnFont()
    {
        var active = ActiveVm.GetActive();
        if (active is null)
            return;

        var alreadyUsingWorkspaceFont = active.UseDefaultFont
            && string.Equals(active.EditorFontFamily, ActiveVm.EditorFontFamily, StringComparison.Ordinal)
            && Math.Abs(active.EditorFontSize - ActiveVm.EditorFontSize) < 0.001
            && active.EditorFontStyle == ActiveVm.DefaultEditorFontStyle
            && active.EditorFontWeight == ActiveVm.DefaultEditorFontWeight;
        if (alreadyUsingWorkspaceFont)
            return;

        ActiveVm.PrepareForRichContent();
        active.EditorFontFamily = ActiveVm.EditorFontFamily;
        active.EditorFontSize = ActiveVm.EditorFontSize;
        active.EditorFontStyle = ActiveVm.DefaultEditorFontStyle;
        active.EditorFontWeight = ActiveVm.DefaultEditorFontWeight;
        active.UseDefaultFont = true;
        ActiveVm.RefreshStatus();
    }

    private void SetActiveColumnTextColor(string value)
    {
        var active = ActiveVm.GetActive();
        if (active is null)
            return;

        var normalized = ColumnTextColorService.Normalize(value);
        if (string.Equals(active.EditorTextColor, normalized, StringComparison.Ordinal))
            return;

        ActiveVm.PrepareForRichContent();
        active.EditorTextColor = normalized;
        ActiveVm.RefreshStatus();
        ActiveVm.StatusText = normalized == ColumnTextColorService.ThemeDefault
            ? $"{active.Title} now uses the theme text colour."
            : $"Set {active.Title} text colour to {normalized}.";
    }

    private void SetActiveColumnCustomTextColor()
    {
        var active = ActiveVm.GetActive();
        if (active is null)
            return;

        var current = ColumnTextColorService.IsCustom(active.EditorTextColor)
            ? active.EditorTextColor
            : "#245A9A";
        var prompt = PromptDialog.Show(
            this,
            "Custom Text Colour",
            "Hex colour (#RRGGBB):",
            current);
        if (string.IsNullOrWhiteSpace(prompt))
            return;

        if (!ColumnTextColorService.TryNormalizeCustomHex(prompt, out var customHex))
        {
            ActiveVm.StatusText = "Custom text colour must use six hexadecimal digits, for example #245A9A.";
            return;
        }

        SetActiveColumnTextColor(customHex);
    }
}
