using Brickwork.Core.Geometry;
using Brickwork.Core.Models;

namespace Brickwork.Exporters.Foundry;

public sealed class FoundryWorldSceneImporter
{
    private readonly FoundryWorldCatalog _catalog;

    public FoundryWorldSceneImporter(FoundryWorldCatalog? catalog = null)
    {
        _catalog = catalog ?? new FoundryWorldCatalog();
    }

    public FoundryWorldCatalog Catalog => _catalog;

    public MapDocument ImportScene(
        string dataRoot,
        string worldId,
        string sceneId,
        string? foundryMapsDirectory = null,
        double? wallSimplificationTolerance = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(worldId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sceneId);

        var scene = _catalog.LoadAssembledScene(dataRoot, worldId, sceneId);
        var map = FoundrySceneMapper.MapFromJson(
            scene.ToJsonString(),
            new FoundrySceneMapContext(
                DataRoot: dataRoot,
                FoundryMapsDirectory: foundryMapsDirectory,
                WallSimplificationTolerance: wallSimplificationTolerance
                    ?? WallSimplificationSettings.DefaultToleranceSceneUnits));

        map.SourceFileName = $"foundry:{worldId}/{sceneId}";
        return map;
    }
}
