using System.Text.Json;
using InkarnateTools.Core.Models;
using InkarnateTools.Inkarnate.Parsing;

namespace InkarnateTools.Inkarnate.Handlers;

internal sealed class LayerAddTransactionHandler : IInkTransactionHandler
{
    public string CommandType => "cmd-layer-add";

    public TransactionAnalysis Process(InkImportContext context, JsonElement transaction)
    {
        var layerId = InkJsonReader.ReadString(transaction, "layerId");
        var layerKind = InkJsonReader.ReadString(transaction, "layerKind");
        if (string.IsNullOrWhiteSpace(layerId))
        {
            var ignoredDetail = layerKind is not null ? $"layer:{layerKind}" : null;
            return TransactionAnalysisFactory.Create(
                transaction,
                CommandType,
                TransactionUnderstanding.KnownIgnored,
                ignoredDetail);
        }
        var name = (string?)null;
        var isVisible = true;

        if (transaction.TryGetProperty("layerData", out var layerData) &&
            layerData.ValueKind == JsonValueKind.Object)
        {
            name = InkJsonReader.ReadString(layerData, "name");
            if (layerData.TryGetProperty("isVisible", out var visibleElement))
            {
                isVisible = visibleElement.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => true,
                };
            }
        }

        var layer = context.EnsureLayer(layerId);
        layer.Kind = layerKind ?? layer.Kind;
        layer.Name = name ?? layer.Name;
        layer.IsVisible = isVisible;

        var detail = layerKind is not null
            ? $"layer:{layerKind} ({layerId})"
            : layerId;

        return TransactionAnalysisFactory.Create(
            transaction,
            CommandType,
            TransactionUnderstanding.FullyUnderstood,
            detail);
    }
}
