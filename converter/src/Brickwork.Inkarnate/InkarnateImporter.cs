using Brickwork.Core.Geometry;
using Brickwork.Core.Models;
using Brickwork.Core.Ports;

namespace Brickwork.Inkarnate;

public sealed class InkarnateImporter : IMapImporter
{
    public string FormatId => "inkarnate";

    public InkarnateImportOptions Options { get; set; } = new();

    /// <summary>Douglas–Peucker tolerance in scene units applied once at the end of import.</summary>
    public double WallSimplificationTolerance
    {
        get => Options.WallSimplificationTolerance;
        set => Options.WallSimplificationTolerance = value;
    }

    public async Task<MapDocument> ImportAsync(Stream source, CancellationToken cancellationToken = default)
    {
        var parsed = await InkarnateFileParser.ParseWithHintsAsync(source, cancellationToken)
            .ConfigureAwait(false);
        WallPointSimplifier.ApplyAll(parsed.Map.Walls, Options.WallSimplificationTolerance);
        RegionImportDetection.PromoteDetectedRegions(parsed.Map, parsed.PathWallTypes, Options);
        LightImportDetection.AddLightsFromStamps(parsed.Map, parsed.Stamps, Options);
        return parsed.Map;
    }
}
