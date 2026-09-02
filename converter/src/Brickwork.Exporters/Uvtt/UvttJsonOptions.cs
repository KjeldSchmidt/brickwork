using System.Text.Json;

namespace Brickwork.Exporters.Uvtt;

internal static class UvttJsonOptions
{
    public static JsonSerializerOptions Create() =>
        new()
        {
            PropertyNamingPolicy = null,
            WriteIndented = false,
        };
}
