namespace Brickwork.Core.Models;

/// <summary>A point light, e.g. created from a lit lantern stamp in an Inkarnate map.</summary>
public sealed class LightSource
{
    public const double DefaultBrightRadius = 4;

    public const double DefaultDimRadius = 8;

    /// <summary>Black means "no colorization" (Foundry light color unset).</summary>
    public const string DefaultColor = LightColors.None;

    public int EntityId { get; init; }

    public string? Name { get; set; }

    /// <summary>Asset name of the source stamp (Inkarnate <c>defaultName</c>), used to group similar lights.</summary>
    public string? SourceName { get; set; }

    public string? LayerId { get; set; }

    public int? GroupId { get; set; }

    public bool IsActive { get; set; } = true;

    public MapPoint Position { get; set; }

    /// <summary>Bright light radius in grid squares.</summary>
    public double BrightRadius { get; set; } = DefaultBrightRadius;

    /// <summary>Dim light radius in grid squares (measured from the light, not from the bright edge).</summary>
    public double DimRadius { get; set; } = DefaultDimRadius;

    /// <summary>Light tint as <c>#rrggbb</c>.</summary>
    public string Color { get; set; } = DefaultColor;

    public LightAnimation Animation { get; set; }

    /// <summary>Animation speed, 1 (slow) to 10 (fast).</summary>
    public int AnimationSpeed { get; set; } = LightAnimations.DefaultSpeed;

    /// <summary>Animation intensity, 1 (subtle) to 10 (strong).</summary>
    public int AnimationIntensity { get; set; } = LightAnimations.DefaultIntensity;

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "Light" : Name;

    /// <summary>Key used to group lights of the same kind in the editor.</summary>
    public string KindName => string.IsNullOrWhiteSpace(SourceName) ? DisplayName : SourceName.Trim();
}
