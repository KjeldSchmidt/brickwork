using System.Text.Json;

namespace Brickwork.Inkarnate.Parsing;

internal static class InkPreviewImageReader
{
    public static byte[]? ReadPreviewImagePng(JsonElement root)
    {
        if (!root.TryGetProperty("preview", out var previewElement) ||
            previewElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var dataUrl = previewElement.GetString();
        if (string.IsNullOrWhiteSpace(dataUrl))
        {
            return null;
        }

        const string prefix = "data:image/png;base64,";
        if (!dataUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var base64 = dataUrl[prefix.Length..];
        try
        {
            return Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
