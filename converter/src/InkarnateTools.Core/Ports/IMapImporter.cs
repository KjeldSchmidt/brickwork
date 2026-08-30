using InkarnateTools.Core.Models;

namespace InkarnateTools.Core.Ports;

public interface IMapImporter
{
    string FormatId { get; }

    Task<MapDocument> ImportAsync(Stream source, CancellationToken cancellationToken = default);
}
