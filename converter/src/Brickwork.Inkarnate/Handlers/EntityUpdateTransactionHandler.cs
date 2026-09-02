using System.Text.Json;
using Brickwork.Core.Models;
using Brickwork.Inkarnate.Parsing;

namespace Brickwork.Inkarnate.Handlers;

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

        if (!item.TryGetProperty("update", out var updateElement) ||
            updateElement.ValueKind != JsonValueKind.Object)
        {
            return TransactionUnderstanding.KnownIgnored;
        }

        if (context.GroupsById.TryGetValue(entityId.Value, out var group))
        {
            return ApplyGroupUpdate(context, group, updateElement);
        }

        if (!context.WallsByEntityId.TryGetValue(entityId.Value, out var wall))
        {
            return TransactionUnderstanding.KnownIgnored;
        }

        return ApplyWallUpdate(wall, updateElement);
    }

    private static TransactionUnderstanding ApplyGroupUpdate(
        InkImportContext context,
        EntityGroup group,
        JsonElement updateElement)
    {
        var understanding = TransactionUnderstanding.FullyUnderstood;
        var appliedKnownField = false;
        double? x = null;
        double? y = null;
        double? angle = null;
        double? originX = null;
        double? originY = null;
        var hasTransform = false;

        foreach (var property in updateElement.EnumerateObject())
        {
            switch (property.Name)
            {
                case "name":
                    group.Name = property.Value.GetString();
                    appliedKnownField = true;
                    break;
                case "x":
                    if (property.Value.ValueKind == JsonValueKind.Number)
                    {
                        x = property.Value.GetDouble();
                        hasTransform = true;
                        appliedKnownField = true;
                    }

                    break;
                case "y":
                    if (property.Value.ValueKind == JsonValueKind.Number)
                    {
                        y = property.Value.GetDouble();
                        hasTransform = true;
                        appliedKnownField = true;
                    }

                    break;
                case "angle":
                    if (property.Value.ValueKind == JsonValueKind.Number)
                    {
                        angle = property.Value.GetDouble();
                        hasTransform = true;
                        appliedKnownField = true;
                    }

                    break;
                case "oX":
                    if (property.Value.ValueKind == JsonValueKind.Number)
                    {
                        originX = property.Value.GetDouble();
                        hasTransform = true;
                        appliedKnownField = true;
                    }

                    break;
                case "oY":
                    if (property.Value.ValueKind == JsonValueKind.Number)
                    {
                        originY = property.Value.GetDouble();
                        hasTransform = true;
                        appliedKnownField = true;
                    }

                    break;
                case "z":
                case "order":
                    appliedKnownField = true;
                    break;
                default:
                    understanding = TransactionUnderstanding.KnownIgnored;
                    break;
            }
        }

        if (hasTransform)
        {
            WallGeometryRebuilder.ApplyGroupTransform(context, group, x, y, angle, originX, originY);
        }

        return appliedKnownField ? understanding : TransactionUnderstanding.KnownIgnored;
    }

    private static TransactionUnderstanding ApplyWallUpdate(Wall wall, JsonElement updateElement)
    {
        var understanding = TransactionUnderstanding.FullyUnderstood;
        var appliedKnownField = false;
        double? x = null;
        double? y = null;
        double? angle = null;
        double? originX = null;
        double? originY = null;
        double? scale = null;
        var hasTransform = false;
        var pathsChanged = false;

        foreach (var property in updateElement.EnumerateObject())
        {
            switch (property.Name)
            {
                case "name":
                    wall.Name = property.Value.GetString();
                    appliedKnownField = true;
                    break;
                case "portals":
                    EntityParsing.ApplyPortals(wall, property.Value);
                    appliedKnownField = true;
                    break;
                case "wallThickness":
                    wall.WallThickness = property.Value.ValueKind == JsonValueKind.Number
                        ? property.Value.GetDouble()
                        : 0;
                    appliedKnownField = true;
                    break;
                case "paths":
                    wall.PathData = property.Value.GetString();
                    pathsChanged = true;
                    appliedKnownField = true;
                    break;
                case "isClosedPath":
                    wall.IsClosed = property.Value.ValueKind == JsonValueKind.True;
                    appliedKnownField = true;
                    break;
                case "x":
                    if (property.Value.ValueKind == JsonValueKind.Number)
                    {
                        x = property.Value.GetDouble();
                        hasTransform = true;
                        appliedKnownField = true;
                    }

                    break;
                case "y":
                    if (property.Value.ValueKind == JsonValueKind.Number)
                    {
                        y = property.Value.GetDouble();
                        hasTransform = true;
                        appliedKnownField = true;
                    }

                    break;
                case "angle":
                    if (property.Value.ValueKind == JsonValueKind.Number)
                    {
                        angle = property.Value.GetDouble();
                        hasTransform = true;
                        appliedKnownField = true;
                    }

                    break;
                case "oX":
                    if (property.Value.ValueKind == JsonValueKind.Number)
                    {
                        originX = property.Value.GetDouble();
                        hasTransform = true;
                        appliedKnownField = true;
                    }

                    break;
                case "oY":
                    if (property.Value.ValueKind == JsonValueKind.Number)
                    {
                        originY = property.Value.GetDouble();
                        hasTransform = true;
                        appliedKnownField = true;
                    }

                    break;
                case "scale":
                    if (property.Value.ValueKind == JsonValueKind.Number)
                    {
                        scale = property.Value.GetDouble();
                        hasTransform = true;
                        appliedKnownField = true;
                    }

                    break;
                case "z":
                case "order":
                    appliedKnownField = true;
                    break;
                default:
                    understanding = TransactionUnderstanding.KnownIgnored;
                    break;
            }
        }

        if (hasTransform)
        {
            WallGeometryRebuilder.ApplyEntityTransform(wall, x, y, angle, originX, originY, scale);
        }

        if (pathsChanged)
        {
            if (!updateElement.TryGetProperty("isClosedPath", out _) &&
                !string.IsNullOrWhiteSpace(wall.PathData))
            {
                wall.IsClosed = InkSvgPathParser.IsClosedPath(wall.PathData);
            }

            WallGeometryRebuilder.RebuildFromPath(wall);
        }

        return appliedKnownField ? understanding : TransactionUnderstanding.KnownIgnored;
    }

    private static TransactionUnderstanding Max(
        TransactionUnderstanding current,
        TransactionUnderstanding candidate) =>
        (TransactionUnderstanding)Math.Max((int)current, (int)candidate);
}
