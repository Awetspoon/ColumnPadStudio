using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ColumnPadStudio.ViewModels;

public sealed partial class MainViewModel
{
    private static IReadOnlyList<EditorLanguageOption> BuildEditorLanguages()
    {
        var languageTags = new[]
        {
            "en-US",
            "en-GB",
            "fr-FR",
            "de-DE",
            "es-ES",
            "it-IT",
            "pt-BR",
            "pt-PT",
            "nl-NL",
            "sv-SE",
            "da-DK",
            "nb-NO"
        };

        return languageTags
            .Select(tag => new EditorLanguageOption(tag, BuildLanguageDisplayName(tag)))
            .ToList();
    }

    private string NormalizeEditorLanguageTag(string? requestedTag)
    {
        if (!string.IsNullOrWhiteSpace(requestedTag))
        {
            var match = EditorLanguages.FirstOrDefault(language =>
                string.Equals(language.Tag, requestedTag, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match.Tag;
        }

        return EditorLanguages.Count > 0 ? EditorLanguages[0].Tag : "en-US";
    }

    private static string BuildLanguageDisplayName(string tag)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(tag);
            return $"{culture.EnglishName} ({culture.Name})";
        }
        catch (CultureNotFoundException)
        {
            return tag;
        }
    }

    private static IReadOnlyList<string> BuildInstalledFontFamilies()
    {
        var names = Fonts.SystemFontFamilies
            .Select(f => f.Source)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (names.Count == 0)
            names.AddRange(["Consolas", "Segoe UI", "Courier New"]);

        return names;
    }

    private string ResolveInstalledFamily(string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var match = EditorFontFamilies.FirstOrDefault(f => string.Equals(f, requested, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match))
                return match;
        }

        return EditorFontFamilies.Count > 0 ? EditorFontFamilies[0] : "Consolas";
    }

    private void UpdateFontFaceOptionsForFamily(string familyName, string? preferredStyleName)
    {
        var family = Fonts.SystemFontFamilies.FirstOrDefault(f =>
            string.Equals(f.Source, familyName, StringComparison.OrdinalIgnoreCase));

        family ??= new FontFamily(EditorFontFamilies.Count > 0 ? EditorFontFamilies[0] : "Consolas");

        var options = BuildFontFaceOptions(family);

        _fontFaceOptionsByName.Clear();
        EditorFontStyles.Clear();
        foreach (var option in options)
        {
            _fontFaceOptionsByName[option.Name] = option;
            EditorFontStyles.Add(option.Name);
        }

        var desired = preferredStyleName;
        if (string.IsNullOrWhiteSpace(desired) || !_fontFaceOptionsByName.ContainsKey(desired))
            desired = EditorFontStyles.FirstOrDefault() ?? "Regular";

        if (_fontFaceOptionsByName.TryGetValue(desired, out var selected))
        {
            Set(ref _editorFontStyleName, selected.Name);
            _editorFontStyle = selected.Style;
            _editorFontWeight = selected.Weight;
        }
    }

    private static List<FontFaceOption> BuildFontFaceOptions(FontFamily family)
    {
        var options = new Dictionary<string, FontFaceOption>(StringComparer.CurrentCultureIgnoreCase);

        foreach (var typeface in family.GetTypefaces())
        {
            if (!typeface.TryGetGlyphTypeface(out _))
                continue;

            var name = ToStyleName(typeface.Style, typeface.Weight);
            options[name] = new FontFaceOption(name, typeface.Style, typeface.Weight);
        }

        if (options.Count == 0)
            options["Regular"] = new FontFaceOption("Regular", FontStyles.Normal, FontWeights.Normal);

        return options.Values
            .OrderBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static string ToStyleName(FontStyle style, FontWeight weight)
    {
        var parts = new List<string>();

        var weightName = weight.ToOpenTypeWeight() switch
        {
            <= 150 => "Thin",
            <= 250 => "ExtraLight",
            <= 350 => "Light",
            <= 450 => "Regular",
            <= 550 => "Medium",
            <= 650 => "SemiBold",
            <= 750 => "Bold",
            <= 850 => "ExtraBold",
            <= 950 => "Black",
            _ => "ExtraBlack"
        };

        if (!string.Equals(weightName, "Regular", StringComparison.OrdinalIgnoreCase) || style == FontStyles.Normal)
            parts.Add(weightName);

        if (style == FontStyles.Italic)
            parts.Add("Italic");
        else if (style == FontStyles.Oblique)
            parts.Add("Oblique");

        return parts.Count == 0 ? "Regular" : string.Join(' ', parts);
    }

    public sealed record EditorLanguageOption(string Tag, string DisplayName);

    private readonly record struct FontFaceOption(string Name, FontStyle Style, FontWeight Weight);
}
