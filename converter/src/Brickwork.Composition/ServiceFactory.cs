using Brickwork.Core.Ports;
using Brickwork.Core.Services;
using Brickwork.Exporters;
using Brickwork.Exporters.Uvtt;
using Brickwork.Inkarnate;

namespace Brickwork.Composition;

public static class ServiceFactory
{
    public static IMapImporter CreateInkarnateImporter() => new InkarnateImporter();

    public static IMapImporter CreateUvttImporter() => new UvttImporter();

    public static IMapImporter CreateImporterForPath(string path) =>
        UvttImporter.IsUvttPath(path) ? CreateUvttImporter() : CreateInkarnateImporter();

    public static IReadOnlyList<IMapImporter> CreateImporters() =>
        [CreateInkarnateImporter(), CreateUvttImporter()];

    public static ConvertMapService CreateConvertMapService()
    {
        IMapExporter[] exporters =
        [
            new UvttExporter(),
            new Uvtt2Exporter(),
            new FoundryExporter(),
        ];

        return new ConvertMapService(CreateImporters(), exporters);
    }

    public static ConvertMapService CreateGuiConvertMapService() =>
        new(CreateImporters(), [new UvttExporter(), new FoundryExporter()]);

    public static IInkFileAnalyzer CreateInkFileAnalyzer() =>
        new InkarnateCompatibilityAnalyzer(CreateInkarnateImporter());
}
