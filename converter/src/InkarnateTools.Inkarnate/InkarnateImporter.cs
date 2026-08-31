using InkarnateTools.Core.Models;
using InkarnateTools.Core.Ports;

namespace InkarnateTools.Inkarnate;

public sealed class InkarnateImporter : IMapImporter
{
    public string FormatId => "inkarnate";

    public Task<MapDocument> ImportAsync(Stream source, CancellationToken cancellationToken = default) =>
        InkarnateFileParser.ParseAsync(source, cancellationToken);
}
