using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;

namespace ColumnPadStudio.ViewModels;

public sealed partial class MainViewModel
{
    private static FontStyle ParseFontStyle(string? value, FontStyle fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        try
        {
            var converter = new FontStyleConverter();
            if (converter.ConvertFromString(value) is FontStyle parsed)
                return parsed;
        }
        catch (FormatException)
        {
            // Fallback to existing font style when persisted value is invalid.
        }
        catch (NotSupportedException)
        {
            // Fallback to existing font style when persisted value is invalid.
        }

        return fallback;
    }

    private static FontWeight ParseFontWeight(string? value, FontWeight fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        try
        {
            var converter = new FontWeightConverter();
            if (converter.ConvertFromString(value) is FontWeight parsed)
                return parsed;
        }
        catch (FormatException)
        {
            // Fallback to existing font weight when persisted value is invalid.
        }
        catch (NotSupportedException)
        {
            // Fallback to existing font weight when persisted value is invalid.
        }

        return fallback;
    }

    private static T GetJsonValueOrDefault<T>(JsonObject? node, string propertyName, T fallback)
    {
        if (node is not null &&
            node[propertyName] is JsonValue valueNode &&
            valueNode.TryGetValue<T>(out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private static double GetJsonDoubleOrDefault(JsonObject? node, string propertyName, double fallback)
    {
        if (node is null || node[propertyName] is not JsonValue valueNode)
            return fallback;

        if (valueNode.TryGetValue<double>(out var asDouble))
            return asDouble;

        if (valueNode.TryGetValue<int>(out var asInt))
            return asInt;

        return fallback;
    }

    private static int? GetJsonNullableInt(JsonObject? node, string propertyName)
    {
        if (node is not null &&
            node[propertyName] is JsonValue valueNode &&
            valueNode.TryGetValue<int>(out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static List<int> GetJsonIntArray(JsonObject? node, string propertyName)
    {
        if (node is null || node[propertyName] is not JsonArray values)
            return [];

        var parsed = new List<int>(values.Count);
        foreach (var value in values)
        {
            if (value is JsonValue valueNode && valueNode.TryGetValue<int>(out var lineIndex) && lineIndex >= 0)
                parsed.Add(lineIndex);
        }

        return NormalizeCheckedChecklistLineIndexes(parsed);
    }
}
