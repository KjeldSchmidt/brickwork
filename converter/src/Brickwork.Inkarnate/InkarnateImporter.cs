using Brickwork.Core.Geometry;
using Brickwork.Core.Models;
using Brickwork.Core.Ports;

namespace Brickwork.Inkarnate;

public sealed class InkarnateImporter : IMapImporter
{
    public string FormatId => "inkarnate";

    /// <summary>Douglas–Peucker tolerance in scene units applied once at the end of import.</summary>
    public double WallSimplificationTolerance { get; set; } =
        WallSimplificationSettings.DefaultToleranceSceneUnits;

    public async Task<MapDocument> ImportAsync(Stream source, CancellationToken cancellationToken = default)
    {
        var map = await InkarnateFileParser.ParseAsync(source, cancellationToken).ConfigureAwait(false);
        WallPointSimplifier.ApplyAll(map.Walls, WallSimplificationTolerance);
        return map;
    }
}
