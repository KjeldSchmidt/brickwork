using InkarnateTools.Core.Models;

namespace InkarnateTools.Core.Ports;

public interface IMapExporter
{
    string FormatId { get; }

    Task ExportAsync(MapDocument map, Stream destination, CancellationToken cancellationToken = default);
}
