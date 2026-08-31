using System.Text.Json;
using InkarnateTools.Core.Models;
using InkarnateTools.Core.Ports;

namespace InkarnateTools.Exporters;

public abstract class PlaceholderExporterBase : IMapExporter
{
    public abstract string FormatId { get; }

    public Task ExportAsync(MapDocument map, Stream destination, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            format = FormatId,
            map = new
            {
                map.Id,
                map.Name,
                map.SourceVersion,
                scene = map.Scene,
                preview = map.Preview,
                grid = map.Grid,
                wallCount = map.ExportableWalls().Count(),
                lightCount = map.Lights.Count,
            },
            note = "Placeholder export — real serialization not implemented yet.",
        };

        return JsonSerializer.SerializeAsync(
            destination,
            payload,
            new JsonSerializerOptions { WriteIndented = true },
            cancellationToken);
    }
}

public sealed class Uvtt1Exporter : PlaceholderExporterBase
{
    public override string FormatId => "uvtt1";
}

public sealed class Uvtt2Exporter : PlaceholderExporterBase
{
    public override string FormatId => "uvtt2";
}
