using System.Text.Json;
using Brickwork.Core.Models;
using Brickwork.Inkarnate.Parsing;

namespace Brickwork.Inkarnate.Handlers;

internal sealed class EntityMoveToLayerTransactionHandler : IInkTransactionHandler
{
    public string CommandType => "cmd-entity-move-to-layer";

    public TransactionAnalysis Process(InkImportContext context, JsonElement transaction)
    {
        var targetLayerId = InkJsonReader.ReadString(transaction, "targetLayerId");
        if (string.IsNullOrWhiteSpace(targetLayerId))
        {
            return TransactionAnalysisFactory.Create(
                transaction,
                CommandType,
                TransactionUnderstanding.Unknown,
                "missing targetLayerId");
        }

        var entityIds = ReadEntityIds(transaction);
        if (entityIds.Count == 0)
        {
            return TransactionAnalysisFactory.Create(
                transaction,
                CommandType,
                TransactionUnderstanding.KnownIgnored,
                "empty entityIds");
        }

        context.EnsureLayer(targetLayerId);
        foreach (var entityId in entityIds)
        {
            context.MoveEntityToLayer(entityId, targetLayerId);
        }

        return TransactionAnalysisFactory.Create(
            transaction,
            CommandType,
            TransactionUnderstanding.FullyUnderstood,
            $"→ {targetLayerId} ({entityIds.Count})");
    }

    private static List<int> ReadEntityIds(JsonElement transaction)
    {
        var ids = new List<int>();
        if (!transaction.TryGetProperty("entityIds", out var idsElement) ||
            idsElement.ValueKind != JsonValueKind.Array)
        {
            return ids;
        }

        foreach (var idElement in idsElement.EnumerateArray())
        {
            if (idElement.ValueKind == JsonValueKind.Number)
            {
                ids.Add(idElement.GetInt32());
            }
        }

        return ids;
    }
}

internal sealed class LayerReorderTransactionHandler : IInkTransactionHandler
{
    public string CommandType => "cmd-layer-reorder";

    public TransactionAnalysis Process(InkImportContext context, JsonElement transaction)
    {
        if (!transaction.TryGetProperty("newLayerOrder", out var orderElement) ||
            orderElement.ValueKind != JsonValueKind.Array)
        {
            return TransactionAnalysisFactory.Create(
                transaction,
                CommandType,
                TransactionUnderstanding.Unknown,
                "missing newLayerOrder");
        }

        var order = new List<string>();
        foreach (var idElement in orderElement.EnumerateArray())
        {
            if (idElement.ValueKind == JsonValueKind.String &&
                idElement.GetString() is { Length: > 0 } layerId)
            {
                order.Add(layerId);
            }
        }

        if (order.Count == 0)
        {
            return TransactionAnalysisFactory.Create(
                transaction,
                CommandType,
                TransactionUnderstanding.KnownIgnored,
                "empty newLayerOrder");
        }

        context.ReorderLayers(order);
        return TransactionAnalysisFactory.Create(
            transaction,
            CommandType,
            TransactionUnderstanding.FullyUnderstood,
            $"{order.Count} layers");
    }
}

internal sealed class LayerUpdateNameTransactionHandler : IInkTransactionHandler
{
    public string CommandType => "cmd-layer-update-name";

    public TransactionAnalysis Process(InkImportContext context, JsonElement transaction)
    {
        var layerId = InkJsonReader.ReadString(transaction, "layerId");
        if (string.IsNullOrWhiteSpace(layerId))
        {
            return TransactionAnalysisFactory.Create(
                transaction,
                CommandType,
                TransactionUnderstanding.Unknown,
                "missing layerId");
        }

        var name = InkJsonReader.ReadString(transaction, "name");
        var layer = context.EnsureLayer(layerId);
        layer.Name = name;

        return TransactionAnalysisFactory.Create(
            transaction,
            CommandType,
            TransactionUnderstanding.FullyUnderstood,
            string.IsNullOrWhiteSpace(name) ? layerId : $"{layerId} → {name}");
    }
}

internal sealed class LayerUpdateVisibilityTransactionHandler : IInkTransactionHandler
{
    public string CommandType => "cmd-layer-update-visibility";

    public TransactionAnalysis Process(InkImportContext context, JsonElement transaction)
    {
        var layerId = InkJsonReader.ReadString(transaction, "layerId");
        if (string.IsNullOrWhiteSpace(layerId))
        {
            return TransactionAnalysisFactory.Create(
                transaction,
                CommandType,
                TransactionUnderstanding.Unknown,
                "missing layerId");
        }

        if (!transaction.TryGetProperty("isVisible", out var visibleElement) ||
            (visibleElement.ValueKind is not JsonValueKind.True and not JsonValueKind.False))
        {
            return TransactionAnalysisFactory.Create(
                transaction,
                CommandType,
                TransactionUnderstanding.Unknown,
                "missing isVisible");
        }

        var isVisible = visibleElement.ValueKind == JsonValueKind.True;
        context.SetLayerVisibility(layerId, isVisible);

        return TransactionAnalysisFactory.Create(
            transaction,
            CommandType,
            TransactionUnderstanding.FullyUnderstood,
            $"{layerId} → {(isVisible ? "visible" : "hidden")}");
    }
}

internal sealed class LayerRemoveTransactionHandler : IInkTransactionHandler
{
    public string CommandType => "cmd-layer-remove";

    public TransactionAnalysis Process(InkImportContext context, JsonElement transaction)
    {
        var layerId = InkJsonReader.ReadString(transaction, "layerId");
        if (string.IsNullOrWhiteSpace(layerId))
        {
            return TransactionAnalysisFactory.Create(
                transaction,
                CommandType,
                TransactionUnderstanding.Unknown,
                "missing layerId");
        }

        var removed = context.RemoveLayer(layerId);
        return TransactionAnalysisFactory.Create(
            transaction,
            CommandType,
            removed ? TransactionUnderstanding.FullyUnderstood : TransactionUnderstanding.KnownIgnored,
            layerId);
    }
}

internal sealed class KnownIgnoredCommandHandler : IInkTransactionHandler
{
    public KnownIgnoredCommandHandler(string commandType, string? detail = null)
    {
        CommandType = commandType;
        _detail = detail;
    }

    private readonly string? _detail;

    public string CommandType { get; }

    public TransactionAnalysis Process(InkImportContext context, JsonElement transaction) =>
        TransactionAnalysisFactory.Create(
            transaction,
            CommandType,
            TransactionUnderstanding.KnownIgnored,
            _detail);
}
