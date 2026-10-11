using Brickwork.Core.Foundry;
using Brickwork.Core.Geometry;

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
}
