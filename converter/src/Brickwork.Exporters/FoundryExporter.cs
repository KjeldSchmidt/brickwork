using Brickwork.Core.Models;
using Brickwork.Core.Ports;
using Brickwork.Exporters.Foundry;

namespace Brickwork.Exporters;

public sealed class FoundryExporter : IMapExporter
{
    public string FormatId => "foundry";

    public Task ExportAsync(MapDocument map, Stream destination, CancellationToken cancellationToken = default) =>
        FoundrySceneBuilder.WriteAsync(map, destination, cancellationToken);
}
