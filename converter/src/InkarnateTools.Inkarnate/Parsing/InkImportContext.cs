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
}
