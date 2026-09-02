using Brickwork.Core.Ports;
using Brickwork.Core.Services;
using Brickwork.Exporters;
using Brickwork.Inkarnate;

namespace Brickwork.Composition;

public static class ServiceFactory
{
    public static IMapImporter CreateInkarnateImporter() => new InkarnateImporter();

    public static ConvertMapService CreateConvertMapService()
    {
        IMapImporter importer = CreateInkarnateImporter();
        IMapExporter[] exporters =
        [
            new Uvtt1Exporter(),
            new Uvtt2Exporter(),
            new FoundryExporter(),
        ];

        return new ConvertMapService(importer, exporters);
    }

    public static ConvertMapService CreateGuiConvertMapService() =>
        new(CreateInkarnateImporter(), [new FoundryExporter()]);

    public static IInkFileAnalyzer CreateInkFileAnalyzer() =>
        new InkarnateCompatibilityAnalyzer(CreateInkarnateImporter());
}
