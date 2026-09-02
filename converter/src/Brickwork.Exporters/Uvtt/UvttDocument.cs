using System.Text.Json.Serialization;

namespace Brickwork.Exporters.Uvtt;

internal sealed class UvttDocument
{
    [JsonPropertyName("software")]
    public string? Software { get; set; }

    [JsonPropertyName("format")]
    public double Format { get; set; } = 1.0;

    [JsonPropertyName("resolution")]
    public UvttResolution Resolution { get; set; } = new();

    [JsonPropertyName("line_of_sight")]
    public List<List<UvttPoint>> LineOfSight { get; set; } = [];

    [JsonPropertyName("objects_line_of_sight")]
    public List<List<UvttPoint>> ObjectsLineOfSight { get; set; } = [];

    [JsonPropertyName("portals")]
    public List<UvttPortal> Portals { get; set; } = [];

    [JsonPropertyName("environment")]
    public UvttEnvironment Environment { get; set; } = new();

    [JsonPropertyName("lights")]
    public List<object> Lights { get; set; } = [];

    [JsonPropertyName("image")]
    public string Image { get; set; } = string.Empty;
}

internal sealed class UvttResolution
{
    [JsonPropertyName("map_origin")]
    public UvttPoint MapOrigin { get; set; } = new();

    [JsonPropertyName("map_size")]
    public UvttPoint MapSize { get; set; } = new();

    [JsonPropertyName("pixels_per_grid")]
    public int PixelsPerGrid { get; set; } = 70;
}

internal sealed class UvttPoint
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }
}

internal sealed class UvttPortal
{
    [JsonPropertyName("position")]
    public UvttPoint Position { get; set; } = new();

    [JsonPropertyName("bounds")]
    public List<UvttPoint> Bounds { get; set; } = [];

    [JsonPropertyName("rotation")]
    public double Rotation { get; set; }

    [JsonPropertyName("closed")]
    public bool Closed { get; set; } = true;

    [JsonPropertyName("freestanding")]
    public bool Freestanding { get; set; }
}

internal sealed class UvttEnvironment
{
    [JsonPropertyName("baked_lighting")]
    public bool BakedLighting { get; set; }
}
