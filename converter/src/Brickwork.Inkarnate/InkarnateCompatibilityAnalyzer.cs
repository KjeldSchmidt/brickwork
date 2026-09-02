using Brickwork.Core.Models;
using Brickwork.Core.Ports;

namespace Brickwork.Inkarnate;

public sealed class InkarnateCompatibilityAnalyzer : IInkFileAnalyzer
{
    private readonly IMapImporter _importer;

    public InkarnateCompatibilityAnalyzer(IMapImporter importer)
    {
        _importer = importer;
    }

    public InkarnateCompatibilityAnalyzer()
        : this(new InkarnateImporter())
    {
    }

    public string FormatId => _importer.FormatId;

    public async Task<CompatibilityReport> AnalyzeAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        var map = await _importer.ImportAsync(source, cancellationToken).ConfigureAwait(false);
        return map.Compatibility ?? new CompatibilityReport
        {
            MapTitle = map.Name,
            SourceVersion = map.SourceVersion,
            Transactions = [],
        };
    }
}
