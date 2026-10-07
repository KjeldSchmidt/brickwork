namespace Brickwork.Core.Models;

public sealed class Region
{
    public int EntityId { get; init; }

    public string? Name { get; set; }

    public string? LayerId { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsEntityVisible { get; set; } = true;

    public RegionType RegionType { get; set; } = RegionType.DifficultTerrain;

    public int? GroupId { get; set; }

    /// <summary>Closed scene-space polygon. Always treated as closed; first and last points need not coincide.</summary>
    public IList<MapPoint> Points { get; init; } = [];

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "Region" : Name;
}
