using Brickwork.Core.Geometry;
using Brickwork.Core.Models;

namespace Brickwork.Inkarnate;

public sealed class InkarnateImportOptions
{
    public double WallSimplificationTolerance { get; set; } =
        WallSimplificationSettings.DefaultToleranceSceneUnits;

    public bool DetectRegionsFromColorPaths { get; set; } = true;

    public bool DetectRegionsFromNamedContainers { get; set; } = true;

    public bool DetectRegionsFromPathNames { get; set; } = true;

    /// <summary>Create lights from stamps whose names match <see cref="LightKeywords"/>.</summary>
    public bool DetectLightsFromStamps { get; set; } = true;

    public string LightKeywords { get; set; } = Core.Models.LightKeywords.DefaultInclude;

    public string LightExcludeKeywords { get; set; } = Core.Models.LightKeywords.DefaultExclude;

    /// <summary>Pick a flicker/pulse animation for detected lights from their names.</summary>
    public bool DetectLightAnimations { get; set; } = true;

    /// <summary>Comma-separated words that map a light name to the Torch animation.</summary>
    public string LightTorchWords { get; set; } = LightAnimations.DefaultTorchWords;

    /// <summary>Comma-separated words that map a light name to the Flicker animation.</summary>
    public string LightFlickerWords { get; set; } = LightAnimations.DefaultFlickerWords;

    /// <summary>Comma-separated words that map a light name to the Pulse animation.</summary>
    public string LightPulseWords { get; set; } = LightAnimations.DefaultPulseWords;

    /// <summary>Bright radius in grid squares for detected lights.</summary>
    public double LightBrightRadius { get; set; } = LightSource.DefaultBrightRadius;

    /// <summary>Dim radius in grid squares for detected lights.</summary>
    public double LightDimRadius { get; set; } = LightSource.DefaultDimRadius;

    public bool AnyRegionDetectionEnabled =>
        DetectRegionsFromColorPaths ||
        DetectRegionsFromNamedContainers ||
        DetectRegionsFromPathNames;
}
