using System.Text.Json;
using InkarnateTools.Core.Models;
using InkarnateTools.Inkarnate.Parsing;

namespace InkarnateTools.Inkarnate.Handlers;

internal sealed class LayerAddTransactionHandler : IInkTransactionHandler
{
    public string CommandType => "cmd-layer-add";

    public TransactionAnalysis Process(InkImportContext context, JsonElement transaction)
    {
        var layerKind = InkJsonReader.ReadString(transaction, "layerKind");
        var layerId = InkJsonReader.ReadString(transaction, "layerId");
        var detail = layerKind is not null && layerId is not null
            ? $"layer:{layerKind} ({layerId})"
            : layerKind is not null ? $"layer:{layerKind}" : layerId;

        return TransactionAnalysisFactory.Create(
            transaction,
            CommandType,
            TransactionUnderstanding.KnownIgnored,
            detail);
    }
}
