using Brickwork.Core.Models;

namespace Brickwork.Core.Ports;

public interface IMapExporter
{
    string FormatId { get; }

    Task ExportAsync(MapDocument map, Stream destination, CancellationToken cancellationToken = default);
}
