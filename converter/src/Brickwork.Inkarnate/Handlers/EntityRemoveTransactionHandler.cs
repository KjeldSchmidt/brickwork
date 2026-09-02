using System.Text.Json;
using Brickwork.Core.Models;
using Brickwork.Inkarnate.Parsing;

namespace Brickwork.Inkarnate.Handlers;

internal sealed class EntityRemoveTransactionHandler : IInkTransactionHandler
{
    public string CommandType => "cmd-entity-remove";

    public TransactionAnalysis Process(InkImportContext context, JsonElement transaction)
    {
        if (!transaction.TryGetProperty("entityIds", out var idsElement) ||
            idsElement.ValueKind != JsonValueKind.Array)
        {
            return TransactionAnalysisFactory.Create(
                transaction,
                CommandType,
                TransactionUnderstanding.Unknown,
                "missing entityIds");
        }

        var removedAny = false;
        foreach (var idElement in idsElement.EnumerateArray())
        {
            if (idElement.ValueKind != JsonValueKind.Number)
            {
                continue;
            }

            var entityId = idElement.GetInt32();
            if (entityId > 0 && context.RemoveEntity(entityId))
            {
                removedAny = true;
            }
        }

        return TransactionAnalysisFactory.Create(
            transaction,
            CommandType,
            removedAny ? TransactionUnderstanding.FullyUnderstood : TransactionUnderstanding.KnownIgnored);
    }
}
