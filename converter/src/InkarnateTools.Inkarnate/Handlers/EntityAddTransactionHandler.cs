using System.Text.Json;
using InkarnateTools.Core.Models;
using InkarnateTools.Inkarnate.Parsing;

namespace InkarnateTools.Inkarnate.Handlers;

internal sealed class EntityAddTransactionHandler : IInkTransactionHandler
{
    private readonly IReadOnlyDictionary<string, IInkEntityHandler> _entityHandlers;

    public EntityAddTransactionHandler(IEnumerable<IInkEntityHandler> entityHandlers)
    {
        _entityHandlers = entityHandlers.ToDictionary(handler => handler.EntityType, StringComparer.OrdinalIgnoreCase);
    }

    public string CommandType => "cmd-entity-add";

    public TransactionAnalysis Process(InkImportContext context, JsonElement transaction)
    {
        if (!transaction.TryGetProperty("items", out var itemsElement) ||
            itemsElement.ValueKind != JsonValueKind.Array)
        {
            return TransactionAnalysisFactory.Create(
                transaction,
                CommandType,
                TransactionUnderstanding.Unknown,
                "missing items");
        }

        var entityTypes = new List<string>();
        var understanding = TransactionUnderstanding.FullyUnderstood;

        foreach (var item in itemsElement.EnumerateArray())
        {
            if (!item.TryGetProperty("entity", out var entityElement))
            {
                entityTypes.Add("(missing entity)");
                understanding = Max(understanding, TransactionUnderstanding.Unknown);
                continue;
            }

            var entityType = InkJsonReader.ReadString(entityElement, "entityType") ?? "(missing entityType)";
            entityTypes.Add(entityType);

            if (!_entityHandlers.TryGetValue(entityType, out var handler))
            {
                understanding = Max(understanding, TransactionUnderstanding.Unknown);
                continue;
            }

            var layerId = InkJsonReader.ReadString(item, "layerId");
            handler.Apply(context, new InkEntityItem(layerId, entityElement));
            understanding = Max(understanding, handler.Understanding);
        }

        if (entityTypes.Count == 0)
        {
            return TransactionAnalysisFactory.Create(
                transaction,
                CommandType,
                TransactionUnderstanding.Unknown,
                "empty items");
        }

        var detail = string.Join(", ", entityTypes.Select(type => $"entity:{type}"));
        return TransactionAnalysisFactory.Create(transaction, CommandType, understanding, detail);
    }

    private static TransactionUnderstanding Max(
        TransactionUnderstanding current,
        TransactionUnderstanding candidate) =>
        (TransactionUnderstanding)Math.Max((int)current, (int)candidate);
}
