using Brickwork.Core.Geometry;

namespace Brickwork.Inkarnate;

public sealed class InkarnateImportOptions
{
    public double WallSimplificationTolerance { get; set; } =
        WallSimplificationSettings.DefaultToleranceSceneUnits;

    public bool DetectRegionsFromColorPaths { get; set; } = true;

    public bool DetectRegionsFromNamedContainers { get; set; } = true;

    public bool DetectRegionsFromPathNames { get; set; } = true;

    public bool AnyRegionDetectionEnabled =>
        DetectRegionsFromColorPaths ||
        DetectRegionsFromNamedContainers ||
        DetectRegionsFromPathNames;
}
