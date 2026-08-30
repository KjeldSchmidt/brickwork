using InkarnateTools.Core.Ports;

namespace InkarnateTools.Core.Services;

public sealed class ConvertMapService
{
    private readonly IMapImporter _importer;
    private readonly IReadOnlyDictionary<string, IMapExporter> _exporters;

    public ConvertMapService(IMapImporter importer, IEnumerable<IMapExporter> exporters)
    {
        ArgumentNullException.ThrowIfNull(importer);
        ArgumentNullException.ThrowIfNull(exporters);

        _importer = importer;
        _exporters = exporters.ToDictionary(exporter => exporter.FormatId, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> SupportedExportFormats =>
        _exporters.Keys.OrderBy(format => format, StringComparer.OrdinalIgnoreCase).ToList();

    public async Task ConvertAsync(
        Stream input,
        Stream output,
        string exportFormatId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        if (string.IsNullOrWhiteSpace(exportFormatId))
        {
            throw new ArgumentException("Export format is required.", nameof(exportFormatId));
        }

        if (!_exporters.TryGetValue(exportFormatId, out var exporter))
        {
            throw new ArgumentException(
                $"Unknown export format '{exportFormatId}'. Supported formats: {string.Join(", ", SupportedExportFormats)}.",
                nameof(exportFormatId));
        }

        var map = await _importer.ImportAsync(input, cancellationToken).ConfigureAwait(false);
        await exporter.ExportAsync(map, output, cancellationToken).ConfigureAwait(false);
    }
}
