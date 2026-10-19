using Brickwork.Core.Foundry;
using Brickwork.Core.Geometry;
using Brickwork.Core.Models;

namespace Brickwork.Core.Settings;

public sealed class AppSettings
{
    public double WallSimplificationTolerance { get; set; } =
        WallSimplificationSettings.DefaultToleranceSceneUnits;

    public bool ShowDebugPanel { get; set; }

    /// <summary>Absolute Foundry maps folder; used as the base for Foundry background.src paths.</summary>
    public string FoundryMapsPath { get; set; } = FoundryPaths.TryGetPreferredMapsDirectory() ?? string.Empty;

    /// <summary>Import closed color (non-texture) paths as regions.</summary>
    public bool DetectRegionsFromColorPaths { get; set; } = true;

    /// <summary>Import closed paths in region-named layers/groups as regions.</summary>
    public bool DetectRegionsFromNamedContainers { get; set; } = true;

    /// <summary>Import closed paths with region-like names as regions.</summary>
    public bool DetectRegionsFromPathNames { get; set; } = true;

    /// <summary>Create lights from stamps whose names match <see cref="LightKeywords"/>.</summary>
    public bool DetectLightsFromStamps { get; set; } = true;

    /// <summary>Comma-separated words that mark a stamp as a light source.</summary>
    public string LightKeywords { get; set; } = Models.LightKeywords.DefaultInclude;

    /// <summary>Comma-separated words that rule a stamp out as a light source (e.g. "broken").</summary>
    public string LightExcludeKeywords { get; set; } = Models.LightKeywords.DefaultExclude;

    /// <summary>Pick a flicker/pulse animation for detected lights from their names.</summary>
    public bool DetectLightAnimations { get; set; } = true;

    /// <summary>Default bright radius, in grid squares, for detected lights.</summary>
    public double LightBrightRadius { get; set; } = LightSource.DefaultBrightRadius;

    /// <summary>Default dim radius, in grid squares, for detected lights.</summary>
    public double LightDimRadius { get; set; } = LightSource.DefaultDimRadius;

    /// <summary>
    /// Highest release version the user chose to skip (e.g. 1.2.0).
    /// Versions at or below this are not offered again by the startup notifier.
    /// </summary>
    public string? SkippedUpdateVersion { get; set; }
}
