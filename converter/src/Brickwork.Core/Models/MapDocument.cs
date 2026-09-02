namespace Brickwork.Core.Models;

public sealed class MapDocument
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; set; } = "Untitled Map";

    public int? SourceVersion { get; set; }

    public SceneDimensions Scene { get; set; } = new();

    public PreviewDimensions? Preview { get; set; }

    public byte[]? PreviewImagePng { get; set; }

    public string? ImagePath { get; set; }

    /// <summary>Original input file name (e.g. <c>basic-walls.ink</c>), used by exporters for asset paths.</summary>
    public string? SourceFileName { get; set; }

    public GridInfo Grid { get; set; } = new();

    public IList<MapLayer> Layers { get; init; } = [];

    public IList<Wall> Walls { get; init; } = [];

    public IList<EntityGroup> Groups { get; init; } = [];

    public IList<LightSource> Lights { get; init; } = [];

    public CompatibilityReport? Compatibility { get; set; }
}

public sealed class SceneDimensions
{
    public double Width { get; set; }

    public double Height { get; set; }
}

public sealed class PreviewDimensions
{
    public int Width { get; set; }

    public int Height { get; set; }
}

public sealed class GridInfo
{
    public double CellSize { get; set; }

    public int PixelsPerCell { get; set; } = 70;

    public int Columns { get; set; }

    public int Rows { get; set; }
}

public sealed class LightSource
{
    public MapPoint Position { get; set; } = new();

    public double Range { get; set; }

    public string Color { get; set; } = "#ffffff";
}

public readonly record struct MapPoint(double X, double Y);
