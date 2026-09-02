using Brickwork.Core.Models;

namespace Brickwork.Core.Ports;

public interface IMapImporter
{
    string FormatId { get; }

    Task<MapDocument> ImportAsync(Stream source, CancellationToken cancellationToken = default);
}
