using InkarnateTools.Core.Models;
using InkarnateTools.Core.Ports;
using InkarnateTools.Exporters.Foundry;

namespace InkarnateTools.Exporters;

public sealed class FoundryExporter : IMapExporter
{
    public string FormatId => "foundry";

    public Task ExportAsync(MapDocument map, Stream destination, CancellationToken cancellationToken = default) =>
        FoundrySceneBuilder.WriteAsync(map, destination, cancellationToken);
}
