using ColumnPadStudio.Domain.Lists;
using ColumnPadStudio.Services;
using ColumnPadStudio.ViewModels;

namespace ColumnPadStudio.SmokeTests;

internal static class EditorServiceSmokeTests
{
    public static void Run(SmokeTestContext tests)
    {
        var searchColumns = new List<string?>
        {
            "alpha beta",
            "gamma\nalpha",
            string.Empty
        };

        tests.Check(TextSearchService.TryFindNext(searchColumns, "alpha", 0, 0, 0, SearchCursor.Empty, out var firstFind), "Text search service should find the first match from the active column.");
        tests.Check(firstFind.ColumnIndex == 0 && firstFind.CharIndex == 0 && firstFind.LineNumber == 1, "Text search service should report first-column hit coordinates.");
        tests.Check(TextSearchService.TryFindNext(searchColumns, "alpha", 0, 0, 0, new SearchCursor(firstFind.ColumnIndex, firstFind.CharIndex), out var secondFind), "Text search service should advance to the next match after the cursor.");
        tests.Check(secondFind.ColumnIndex == 1 && secondFind.CharIndex == 6 && secondFind.LineNumber == 2, "Text search service should report line/char for cross-column next hit.");
        tests.Check(TextSearchService.TryFindNext(searchColumns, "alpha", 0, 0, 0, new SearchCursor(secondFind.ColumnIndex, secondFind.CharIndex), out var wrappedFind), "Text search service should wrap when searching past the last match.");
        tests.Check(wrappedFind.ColumnIndex == 0 && wrappedFind.CharIndex == 0, "Text search service wrap search should return to the first match.");
        tests.Check(!TextSearchService.TryFindNext(searchColumns, "missing", 0, 0, 0, SearchCursor.Empty, out _), "Text search service should return no hit when the term is absent.");

        var (replacedTextByService, replacementCountByService) = TextSearchService.ReplaceAllWithCount("one One one", "one", "two", StringComparison.CurrentCultureIgnoreCase);
        tests.Check(replacementCountByService == 3, "Text search service replace should count all case-insensitive hits.");
        tests.Check(replacedTextByService == "two two two", "Text search service replace should substitute all hits in order.");
        tests.Check(TextSearchService.ComputeLineNumber("a\nb\nc", 4) == 3, "Text search service should compute 1-based line numbers from character index.");
        tests.Check(TextSearchService.ComputeLineNumber("a\rb\r\nc", 5) == 3, "Text search service should count LF, CRLF, and standalone CR line breaks consistently.");

        var listModeVm = new ColumnViewModel
        {
            Text = "alpha\nbeta",
            LineMarkerMode = LineMarkerMode.Bullets
        };
        tests.Check(listModeVm.LineMarkerMode == LineMarkerMode.Bullets, "Line marker mode should support bullets without mutating text.");
        var initialGutterStateVersion = listModeVm.GutterStateVersion;
        listModeVm.LineMarkerMode = LineMarkerMode.Checklist;
        tests.Check(listModeVm.GutterStateVersion > initialGutterStateVersion, "Changing the gutter mode should invalidate its cached rendering.");
        var checklistGutterStateVersion = listModeVm.GutterStateVersion;
        listModeVm.ToggleChecklistLineChecked(0);
        tests.Check(listModeVm.GutterStateVersion > checklistGutterStateVersion, "Changing a checklist marker should invalidate its cached rendering.");
        tests.Check(listModeVm.IsChecklistLineChecked(0), "Checklist gutter mode should toggle checks without inserting inline symbols.");
        tests.Check(listModeVm.Text == "alpha\nbeta", "Checklist gutter mode should keep body text unchanged.");

        var expectedClipboardLines = string.Join(Environment.NewLine, "one", string.Empty, "two", "three");
        tests.Check(
            ClipboardTextService.NormalizeClipboardText("one\r\r\ntwo\u2028three") == expectedClipboardLines,
            "Clipboard text normalization should preserve every line break while normalizing newline characters.");

        var alternatingBlankPaste = "one\n\n two\n\nthree\n\nfour";
        tests.Check(
            ClipboardTextService.NormalizeClipboardText(alternatingBlankPaste) == alternatingBlankPaste.Replace("\n", Environment.NewLine, StringComparison.Ordinal),
            "Clipboard text normalization should preserve intentional alternating blank rows.");

        tests.Check(
            ClipboardTextService.ApplyPastePreset("alpha\n  beta", PasteListPreset.Bullets) == string.Join(Environment.NewLine, "- alpha", "  - beta"),
            "Clipboard bullet preset should add markdown bullets while preserving indentation.");
        tests.Check(
            ClipboardTextService.ApplyPastePreset("- [x] done\nplain", PasteListPreset.Checklist) == string.Join(Environment.NewLine, "- [x] done", "- [ ] plain"),
            "Clipboard checklist preset should preserve checked checklist rows and add unchecked markers to plain rows.");
        tests.Check(
            ClipboardTextService.ApplyPastePreset("1. ordered", PasteListPreset.Bullets) == "1. ordered",
            "Clipboard paste presets should not rewrite ordered-list prefixes.");

        ImageSafetySmokeTests.Run(tests);
    }
}
