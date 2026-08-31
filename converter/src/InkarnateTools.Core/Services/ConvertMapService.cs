using InkarnateTools.Core.Models;
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

        var map = await _importer.ImportAsync(input, cancellationToken).ConfigureAwait(false);
        await ConvertAsync(map, output, exportFormatId, cancellationToken).ConfigureAwait(false);
    }

    public Task ConvertAsync(
        MapDocument map,
        Stream output,
        string exportFormatId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(map);
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

        return exporter.ExportAsync(map, output, cancellationToken);
    }
}
