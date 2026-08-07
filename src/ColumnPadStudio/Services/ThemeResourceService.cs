using System.Collections;
using System.Windows;

namespace ColumnPadStudio.Services;

public static class ThemeResourceService
{
    private const string ThemeResourceRoot = "pack://application:,,,/ColumnPadStudio;component/Resources/Themes/";

    public static void ApplyTheme(ResourceDictionary resources, string preset)
    {
        ArgumentNullException.ThrowIfNull(resources);

        var themeFile = ThemePresetService.Normalize(preset) switch
        {
            ThemePresetService.DarkPreset => "DarkTheme.xaml",
            ThemePresetService.DefaultPreset => "DefaultTheme.xaml",
            _ => "LightTheme.xaml"
        };

        var palette = new ResourceDictionary
        {
            Source = new Uri(ThemeResourceRoot + themeFile, UriKind.Absolute)
        };

        foreach (DictionaryEntry resource in palette)
            resources[resource.Key] = resource.Value;
    }
}
