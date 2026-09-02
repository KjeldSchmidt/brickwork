using Brickwork.Core.Models;
using Brickwork.Core.Ports;

namespace Brickwork.Inkarnate;

public sealed class InkarnateImporter : IMapImporter
{
    public string FormatId => "inkarnate";

    public Task<MapDocument> ImportAsync(Stream source, CancellationToken cancellationToken = default) =>
        InkarnateFileParser.ParseAsync(source, cancellationToken);
}
