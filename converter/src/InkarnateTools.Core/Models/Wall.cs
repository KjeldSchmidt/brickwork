namespace InkarnateTools.Core.Models;

public sealed class Wall
{
    public int EntityId { get; init; }

    public string? Name { get; set; }

    public string? LayerId { get; set; }

    public bool IsActive { get; set; } = true;

    public WallLineType LineType { get; set; } = WallLineType.Default;

    public bool WallEnabled { get; set; } = true;

    public bool IsClosed { get; set; }

    public string? PathData { get; set; }

    public MapPoint Origin { get; set; }

    /// <summary>Translation applied to local path points before rotation (<c>x</c>/<c>y</c> at angle 0).</summary>
    public MapPoint PathOrigin { get; set; }

    /// <summary>Scene-space pivot used with <see cref="Angle"/> (<c>oX</c>/<c>oY</c>).</summary>
    public MapPoint RotationPivot { get; set; }

    /// <summary>Rotation in degrees from Inkarnate (<c>angle</c>).</summary>
    public double Angle { get; set; }

    public double Scale { get; set; } = 1;

    /// <summary>Entity-local wall thickness from Inkarnate (<c>wallThickness</c>).</summary>
    public double WallThickness { get; set; }

    public int? GroupId { get; set; }

    public IList<MapPoint> RawPoints { get; init; } = [];

    public IList<MapPoint> Points { get; init; } = [];

    public IList<WallPortal> Portals { get; init; } = [];

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"Wall {EntityId}" : Name;

    public double SceneThickness => WallThickness * Scale;
}

public sealed class WallPortal
{
    public string Id { get; set; } = string.Empty;

    public MapPoint Anchor { get; set; }

    public double Width { get; set; }

    public bool IsActive { get; set; } = true;

    public WallLineType LineType { get; set; } = WallLineType.Door;
}
