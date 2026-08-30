using InkarnateTools.Core.Ports;
using InkarnateTools.Core.Services;
using InkarnateTools.Exporters;
using InkarnateTools.Inkarnate;

namespace InkarnateTools.Composition;

public static class ServiceFactory
{
    public static ConvertMapService CreateConvertMapService()
    {
        IMapImporter importer = new InkarnateImporter();
        IMapExporter[] exporters =
        [
            new Uvtt1Exporter(),
            new Uvtt2Exporter(),
            new FoundryExporter(),
        ];

        return new ConvertMapService(importer, exporters);
    }
}
