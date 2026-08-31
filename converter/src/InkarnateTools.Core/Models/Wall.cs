namespace InkarnateTools.Core.Models;

public sealed class Wall
{
    public int EntityId { get; init; }

    public string? Name { get; set; }

    public string? LayerId { get; set; }

    public bool IsActive { get; set; } = true;

    public bool WallEnabled { get; set; } = true;

    public bool IsClosed { get; set; }

    public string? PathData { get; set; }

    public MapPoint Origin { get; set; }

    public double Scale { get; set; } = 1;

    public IList<MapPoint> Points { get; init; } = [];

    public IList<WallPortal> Portals { get; init; } = [];

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"Wall {EntityId}" : Name;
}

public sealed class WallPortal
{
    public string Id { get; set; } = string.Empty;

    public MapPoint Anchor { get; set; }

    public double Width { get; set; }
}
