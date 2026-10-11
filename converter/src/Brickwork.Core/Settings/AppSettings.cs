using Brickwork.Core.Geometry;

namespace Brickwork.Core.Settings;

public sealed class AppSettings
{
    public double WallSimplificationTolerance { get; set; } =
        WallSimplificationSettings.DefaultToleranceSceneUnits;

    public bool ShowDebugPanel { get; set; }

    /// <summary>Placeholder for a future Foundry maps directory preference.</summary>
    public string FoundryMapsPath { get; set; } = string.Empty;
}
