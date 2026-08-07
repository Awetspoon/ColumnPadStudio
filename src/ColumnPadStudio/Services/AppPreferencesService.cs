using System.IO;
using System.Text.Json;
using ColumnPadStudio.Models;

namespace ColumnPadStudio.Services;

public static class AppPreferencesService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string PreferencesPath => Path.Combine(AppStoragePaths.RootDirectory, "app-preferences.json");

    public static AppPreferences Load(string? path = null)
        => Load(out _, path);

    public static AppPreferences Load(out string? warning, string? path = null)
    {
        warning = null;
        var resolvedPath = ResolvePath(path);
        if (!File.Exists(resolvedPath))
            return new AppPreferences();

        try
        {
            var json = File.ReadAllText(resolvedPath);
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("The preferences file did not contain an object.");

            var loaded = JsonSerializer.Deserialize<AppPreferences>(json)
                ?? throw new JsonException("The preferences file did not contain an object.");
            return NormalizeLoadedPreferences(loaded, document.RootElement);
        }
        catch (JsonException)
        {
            var invalidPath = TryQuarantineInvalidFile(resolvedPath);
            warning = invalidPath is null
                ? "Preferences could not be read. Default settings are in use."
                : $"Preferences could not be read. Defaults are in use and the invalid file was kept as {Path.GetFileName(invalidPath)}.";
            return new AppPreferences();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            warning = "Preferences could not be read. Default settings are in use.";
            return new AppPreferences();
        }
    }

    public static void Save(AppPreferences preferences, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        var resolvedPath = ResolvePath(path);
        var normalized = Normalize(preferences);
        AtomicFileWriter.WriteText(resolvedPath, JsonSerializer.Serialize(normalized, JsonOptions));
    }

    private static AppPreferences Normalize(AppPreferences preferences)
    {
        return preferences with
        {
            ThemePreset = ThemePresetService.Normalize(preferences.ThemePreset),
            ColumnSpacingPx = AppPreferences.NormalizeColumnSpacing(preferences.ColumnSpacingPx),
            DefaultColumnWidthPx = AppPreferences.NormalizeDefaultColumnWidth(preferences.DefaultColumnWidthPx)
        };
    }

    private static AppPreferences NormalizeLoadedPreferences(
        AppPreferences preferences,
        JsonElement root)
    {
        var hasSnapPreference = HasProperty(root, nameof(AppPreferences.SnapAllColumnsEnabled));
        var hasSpacingPreference = HasProperty(root, nameof(AppPreferences.ColumnSpacingPx));
        var hasFitPreference = HasProperty(root, nameof(AppPreferences.FitColumnsToWindow));
        var hasDefaultWidthPreference = HasProperty(root, nameof(AppPreferences.DefaultColumnWidthPx));

        var snapAllColumnsEnabled = hasSnapPreference
            ? preferences.SnapAllColumnsEnabled
            : true;

        var fitColumnsToWindow = hasFitPreference
            ? preferences.FitColumnsToWindow
            : false;

        return Normalize(preferences with
        {
            SnapAllColumnsEnabled = snapAllColumnsEnabled,
            ColumnSpacingPx = hasSpacingPreference
                ? preferences.ColumnSpacingPx
                : AppPreferences.DefaultColumnSpacingPx,
            FitColumnsToWindow = fitColumnsToWindow,
            DefaultColumnWidthPx = hasDefaultWidthPreference
                ? preferences.DefaultColumnWidthPx
                : AppPreferences.StandardColumnWidthPx
        });
    }

    private static bool HasProperty(JsonElement root, string propertyName)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string ResolvePath(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? PreferencesPath : path;
    }

    private static string? TryQuarantineInvalidFile(string path)
    {
        var invalidPath = $"{path}.invalid-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}";
        try
        {
            File.Move(path, invalidPath);
            return invalidPath;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
