using System.Text.Json;

namespace Brickwork.Inkarnate.Parsing;

internal static class InkJsonReader
{
    public static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var valueElement) &&
        valueElement.ValueKind == JsonValueKind.String
            ? valueElement.GetString()
            : null;

    public static int? ReadInt(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var valueElement) &&
        valueElement.ValueKind == JsonValueKind.Number &&
        valueElement.TryGetInt32(out var value)
            ? value
            : null;

    public static double ReadDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var valueElement) ||
            valueElement.ValueKind != JsonValueKind.Number)
        {
            return 0;
        }

        return valueElement.GetDouble();
    }

    public static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = ReadString(element, propertyName) ?? string.Empty;
        return !string.IsNullOrEmpty(value);
    }
}
