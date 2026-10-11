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
}
