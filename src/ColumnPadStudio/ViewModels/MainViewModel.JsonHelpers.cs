using System.Text.Json.Nodes;
using System.IO;
using System.Windows;
using System.Windows.Media;
using ColumnPadStudio.Services;

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

    private static List<LayoutImage> ReadLayoutImages(JsonObject? node)
    {
        if (node is null || node[nameof(LayoutColumn.Images)] is not JsonArray values)
            return [];

        var parsed = new List<LayoutImage>(values.Count);
        foreach (var value in values)
        {
            if (value is not JsonObject imageNode)
                continue;

            var filePath = GetJsonValueOrDefault(imageNode, nameof(LayoutImage.FilePath), string.Empty);
            var content = ReadEmbeddedImageContent(imageNode);
            if (string.IsNullOrWhiteSpace(filePath) && content is null)
                continue;

            var originalFileName = GetJsonValueOrDefault(
                imageNode,
                nameof(LayoutImage.OriginalFileName),
                string.IsNullOrWhiteSpace(filePath) ? "Picture" : Path.GetFileName(filePath));
            var width = GetJsonDoubleOrDefault(imageNode, nameof(LayoutImage.Width), 320.0);
            var pixelWidth = GetJsonValueOrDefault(imageNode, nameof(LayoutImage.PixelWidth), 0);
            var pixelHeight = GetJsonValueOrDefault(imageNode, nameof(LayoutImage.PixelHeight), 0);
            var left = GetJsonDoubleOrDefault(imageNode, nameof(LayoutImage.Left), 12.0);
            var top = GetJsonDoubleOrDefault(imageNode, nameof(LayoutImage.Top), 12.0);
            var layer = GetJsonValueOrDefault(
                imageNode,
                nameof(LayoutImage.Layer),
                nameof(ColumnImageLayer.InFrontOfText));

            parsed.Add(new LayoutImage(filePath, originalFileName, width, pixelWidth, pixelHeight, left, top, layer, content));
        }

        return parsed;
    }

    private static byte[]? ReadEmbeddedImageContent(JsonObject imageNode)
    {
        if (imageNode[nameof(LayoutImage.Content)] is not JsonValue contentNode)
            return null;

        if (!contentNode.TryGetValue<string>(out var encodedContent) || string.IsNullOrWhiteSpace(encodedContent))
            return null;

        var maximumEncodedLength = ((ColumnImageFileService.MaxImageFileBytes + 2L) / 3L * 4L) + 4L;
        if (encodedContent.Length > maximumEncodedLength)
            throw new InvalidDataException("Embedded picture data is too large.");

        try
        {
            var content = Convert.FromBase64String(encodedContent);
            if (content.Length == 0 || content.Length > ColumnImageFileService.MaxImageFileBytes)
                throw new InvalidDataException("Embedded picture data is empty or too large.");

            return content;
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException("Embedded picture data is not valid Base64.", ex);
        }
    }

    private static ColumnImageLayer ParseImageLayer(string? value)
        => Enum.TryParse<ColumnImageLayer>(value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed)
            ? parsed
            : ColumnImageLayer.InFrontOfText;
}
