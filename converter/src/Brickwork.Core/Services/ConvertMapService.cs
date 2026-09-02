using Brickwork.Core.Models;
using Brickwork.Core.Ports;

namespace Brickwork.Core.Services;

public sealed class ConvertMapService
{
    private readonly IReadOnlyList<IMapImporter> _importers;
    private readonly IMapImporter _defaultImporter;
    private readonly IReadOnlyDictionary<string, IMapExporter> _exporters;

    public ConvertMapService(IEnumerable<IMapImporter> importers, IEnumerable<IMapExporter> exporters)
    {
        ArgumentNullException.ThrowIfNull(importers);
        ArgumentNullException.ThrowIfNull(exporters);

        _importers = importers.ToList();
        if (_importers.Count == 0)
        {
            throw new ArgumentException("At least one importer is required.", nameof(importers));
        }

        _defaultImporter = _importers.FirstOrDefault(importer =>
            string.Equals(importer.FormatId, "inkarnate", StringComparison.OrdinalIgnoreCase))
            ?? _importers[0];

        _exporters = exporters.ToDictionary(exporter => exporter.FormatId, StringComparer.OrdinalIgnoreCase);
    }

    public ConvertMapService(IMapImporter importer, IEnumerable<IMapExporter> exporters)
        : this([importer], exporters)
    {
    }

    public IReadOnlyList<string> SupportedExportFormats =>
        _exporters.Keys.OrderBy(format => format, StringComparer.OrdinalIgnoreCase).ToList();

    public async Task ConvertAsync(
        Stream input,
        Stream output,
        string exportFormatId,
        string? sourceFileName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var importer = ResolveImporter(sourceFileName);
        var map = await importer.ImportAsync(input, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(sourceFileName))
        {
            map.SourceFileName = sourceFileName;
        }

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

    private IMapImporter ResolveImporter(string? sourceFileName)
    {
        if (string.IsNullOrWhiteSpace(sourceFileName))
        {
            return _defaultImporter;
        }

        var extension = Path.GetExtension(sourceFileName);
        var uvttImporter = _importers.FirstOrDefault(importer =>
            string.Equals(importer.FormatId, "uvtt1", StringComparison.OrdinalIgnoreCase));

        if (uvttImporter is not null && IsUvttExtension(extension))
        {
            return uvttImporter;
        }

        return _defaultImporter;
    }

    private static bool IsUvttExtension(string extension) =>
        extension.Equals(".uvtt", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".dd2vtt", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".df2vtt", StringComparison.OrdinalIgnoreCase);
}
