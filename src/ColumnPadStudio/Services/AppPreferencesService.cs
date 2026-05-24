using System.IO;
using System.Text.Json;
using ColumnPadStudio.Models;

namespace ColumnPadStudio.Services;

public static class AppPreferencesService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string PreferencesPath => Path.Combine(AppStoragePaths.RootDirectory, "app-preferences.json");

    public static AppPreferences Load(string? path = null)
    {
        var resolvedPath = ResolvePath(path);
        if (!File.Exists(resolvedPath))
            return new AppPreferences();

        try
        {
            var json = File.ReadAllText(resolvedPath);
            var loaded = JsonSerializer.Deserialize<AppPreferences>(json);
            return loaded is null
                ? new AppPreferences()
                : new AppPreferences(ThemePresetService.Normalize(loaded.ThemePreset));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppPreferences();
        }
    }

    public static void Save(AppPreferences preferences, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        var resolvedPath = ResolvePath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(resolvedPath)!);

        var normalized = preferences with { ThemePreset = ThemePresetService.Normalize(preferences.ThemePreset) };
        File.WriteAllText(resolvedPath, JsonSerializer.Serialize(normalized, JsonOptions));
    }

    private static string ResolvePath(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? PreferencesPath : path;
    }

}
