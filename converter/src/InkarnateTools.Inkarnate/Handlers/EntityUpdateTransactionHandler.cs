using System.Text.Json;
using InkarnateTools.Core.Models;
using InkarnateTools.Inkarnate.Parsing;

namespace InkarnateTools.Inkarnate.Handlers;

internal sealed class EntityUpdateTransactionHandler : IInkTransactionHandler
{
    public string CommandType => "cmd-entity-update";

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

        var understanding = TransactionUnderstanding.FullyUnderstood;
        var itemCount = 0;

        foreach (var item in itemsElement.EnumerateArray())
        {
            itemCount++;
            understanding = Max(understanding, ApplyUpdate(context, item));
        }

        if (itemCount == 0)
        {
            return TransactionAnalysisFactory.Create(
                transaction,
                CommandType,
                TransactionUnderstanding.Unknown,
                "empty items");
        }

        return TransactionAnalysisFactory.Create(transaction, CommandType, understanding);
    }

    private static TransactionUnderstanding ApplyUpdate(InkImportContext context, JsonElement item)
    {
        var entityId = InkJsonReader.ReadInt(item, "entityId");
        if (entityId is null or <= 0)
        {
            return TransactionUnderstanding.KnownIgnored;
        }

        if (!context.WallsByEntityId.TryGetValue(entityId.Value, out var wall))
        {
            return TransactionUnderstanding.KnownIgnored;
        }

        if (!item.TryGetProperty("update", out var updateElement) ||
            updateElement.ValueKind != JsonValueKind.Object)
        {
            return TransactionUnderstanding.KnownIgnored;
        }

        var understanding = TransactionUnderstanding.FullyUnderstood;
        var appliedKnownField = false;

        foreach (var property in updateElement.EnumerateObject())
        {
            switch (property.Name)
            {
                case "name":
                    wall.Name = property.Value.GetString();
                    appliedKnownField = true;
                    break;
                case "portals":
                    wall.Portals.Clear();
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var portalElement in property.Value.EnumerateArray())
                        {
                            var portal = new WallPortal
                            {
                                Id = InkJsonReader.ReadString(portalElement, "id") ?? string.Empty,
                                Width = InkJsonReader.ReadDouble(portalElement, "width"),
                            };

                            if (portalElement.TryGetProperty("anchor", out var anchorElement))
                            {
                                portal.Anchor = EntityParsing.ReadPoint(anchorElement);
                            }

                            wall.Portals.Add(portal);
                        }
                    }

                    appliedKnownField = true;
                    break;
                case "wallThickness":
                    wall.WallThickness = property.Value.ValueKind == JsonValueKind.Number
                        ? property.Value.GetDouble()
                        : 0;
                    appliedKnownField = true;
                    break;
                default:
                    understanding = TransactionUnderstanding.KnownIgnored;
                    break;
            }
        }

        if (!appliedKnownField)
        {
            return TransactionUnderstanding.KnownIgnored;
        }

        return understanding;
    }

    private static TransactionUnderstanding Max(
        TransactionUnderstanding current,
        TransactionUnderstanding candidate) =>
        (TransactionUnderstanding)Math.Max((int)current, (int)candidate);
}
