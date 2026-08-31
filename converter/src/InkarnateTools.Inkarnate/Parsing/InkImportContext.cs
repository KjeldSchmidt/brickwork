using InkarnateTools.Core.Models;

namespace InkarnateTools.Inkarnate.Parsing;

internal sealed class InkImportContext
{
    public InkImportContext(MapDocument map)
    {
        Map = map;
    }

    public MapDocument Map { get; }

    public IList<TransactionAnalysis> Transactions { get; } = [];

    public Dictionary<int, Wall> WallsByEntityId { get; } = [];

    public void SyncWalls()
    {
        Map.Walls.Clear();
        foreach (var wall in WallsByEntityId.Values.OrderBy(w => w.EntityId))
        {
            Map.Walls.Add(wall);
        }
    }
}
