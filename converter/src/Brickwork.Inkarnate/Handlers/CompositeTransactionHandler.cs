using System.Text.Json;
using Brickwork.Core.Models;
using Brickwork.Inkarnate.Parsing;

namespace Brickwork.Inkarnate.Handlers;

internal sealed class CompositeTransactionHandler : IInkTransactionHandler
{
    private readonly Func<InkImportContext, JsonElement, TransactionAnalysis> _processNested;

    public CompositeTransactionHandler(Func<InkImportContext, JsonElement, TransactionAnalysis> processNested)
    {
        _processNested = processNested;
    }

    public string CommandType => "cmd-composite";

    public TransactionAnalysis Process(InkImportContext context, JsonElement transaction)
    {
        if (!transaction.TryGetProperty("cmds", out var cmdsElement) ||
            cmdsElement.ValueKind != JsonValueKind.Array)
        {
            return TransactionAnalysisFactory.Create(
                transaction,
                CommandType,
                TransactionUnderstanding.Unknown,
                "missing cmds");
        }

        var parentId = InkJsonReader.ReadInt(transaction, "transactionId") ?? -1;
        var children = new List<TransactionAnalysis>();
        var understanding = TransactionUnderstanding.FullyUnderstood;

        foreach (var cmd in cmdsElement.EnumerateArray())
        {
            var nested = _processNested(context, cmd);
            if (nested.TransactionId < 0 && parentId >= 0)
            {
                nested = CloneWithTransactionId(nested, parentId);
            }

            children.Add(nested);
            understanding = Combine(understanding, nested.Understanding);
        }

        if (children.Count == 0)
        {
            return TransactionAnalysisFactory.Create(
                transaction,
                CommandType,
                TransactionUnderstanding.Unknown,
                "empty cmds");
        }

        return TransactionAnalysisFactory.Create(
            transaction,
            CommandType,
            understanding,
            $"{children.Count} cmds",
            children);
    }

    private static TransactionAnalysis CloneWithTransactionId(TransactionAnalysis source, int transactionId) =>
        new()
        {
            TransactionId = transactionId,
            CommandType = source.CommandType,
            Understanding = source.Understanding,
            Detail = source.Detail,
            RawJson = source.RawJson,
            Children = source.Children
                .Select(child => CloneWithTransactionId(child, transactionId))
                .ToList(),
        };

    private static TransactionUnderstanding Combine(
        TransactionUnderstanding current,
        TransactionUnderstanding candidate)
    {
        if (current == TransactionUnderstanding.Unknown ||
            candidate == TransactionUnderstanding.Unknown)
        {
            return TransactionUnderstanding.Unknown;
        }

        if (current == TransactionUnderstanding.FullyUnderstood ||
            candidate == TransactionUnderstanding.FullyUnderstood)
        {
            return TransactionUnderstanding.FullyUnderstood;
        }

        return TransactionUnderstanding.KnownIgnored;
    }
}

internal sealed class EntityGroupTransactionHandler : IInkTransactionHandler
{
    public string CommandType => "cmd-entity-group";

    public TransactionAnalysis Process(InkImportContext context, JsonElement transaction)
    {
        var groupId = InkJsonReader.ReadInt(transaction, "groupId");
        if (groupId is null or <= 0)
        {
            return TransactionAnalysisFactory.Create(
                transaction,
                CommandType,
                TransactionUnderstanding.Unknown,
                "missing groupId");
        }

        var memberIds = ReadEntityIds(transaction);
        if (!context.GroupsById.TryGetValue(groupId.Value, out var group))
        {
            group = new EntityGroup { GroupId = groupId.Value };
            context.GroupsById[groupId.Value] = group;
        }

        foreach (var previousMemberId in group.MemberIds.ToList())
        {
            if (!memberIds.Contains(previousMemberId))
            {
                context.DetachFromParent(previousMemberId);
            }
        }

        group.MemberIds.Clear();
        foreach (var memberId in memberIds)
        {
            if (context.WallsByEntityId.TryGetValue(memberId, out var wall) &&
                wall.GroupId is int previousWallGroup &&
                previousWallGroup != groupId.Value &&
                context.GroupsById.TryGetValue(previousWallGroup, out var previousWallParent))
            {
                previousWallParent.MemberIds.Remove(memberId);
            }
            else if (context.GroupsById.TryGetValue(memberId, out var childGroup) &&
                     childGroup.ParentGroupId is int previousGroupParent &&
                     previousGroupParent != groupId.Value &&
                     context.GroupsById.TryGetValue(previousGroupParent, out var previousGroupParentEntity))
            {
                previousGroupParentEntity.MemberIds.Remove(memberId);
            }

            if (context.WallsByEntityId.TryGetValue(memberId, out wall))
            {
                wall.GroupId = groupId;
                group.LayerId ??= wall.LayerId;
            }
            else if (context.GroupsById.TryGetValue(memberId, out var nestedGroup))
            {
                nestedGroup.ParentGroupId = groupId;
                group.LayerId ??= nestedGroup.LayerId;
            }

            group.MemberIds.Add(memberId);
        }

        return TransactionAnalysisFactory.Create(
            transaction,
            CommandType,
            TransactionUnderstanding.FullyUnderstood,
            $"group {groupId.Value} ({memberIds.Count})");
    }

    private static List<int> ReadEntityIds(JsonElement cmd)
    {
        var ids = new List<int>();
        if (!cmd.TryGetProperty("entityIds", out var idsElement) ||
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

internal sealed class EntityMoveToGroupTransactionHandler : IInkTransactionHandler
{
    public string CommandType => "cmd-entity-move-to-group";

    public TransactionAnalysis Process(InkImportContext context, JsonElement transaction)
    {
        var memberIds = ReadEntityIds(transaction);
        if (memberIds.Count == 0)
        {
            return TransactionAnalysisFactory.Create(
                transaction,
                CommandType,
                TransactionUnderstanding.KnownIgnored,
                "empty entityIds");
        }

        var groupId = InkJsonReader.ReadInt(transaction, "groupId");
        foreach (var memberId in memberIds)
        {
            if (groupId is > 0)
            {
                context.AttachToGroup(memberId, groupId.Value);
            }
            else
            {
                context.DetachFromParent(memberId);
            }
        }

        var detail = groupId is > 0
            ? $"→ group {groupId.Value} ({memberIds.Count})"
            : $"ungroup ({memberIds.Count})";

        return TransactionAnalysisFactory.Create(
            transaction,
            CommandType,
            TransactionUnderstanding.FullyUnderstood,
            detail);
    }

    private static List<int> ReadEntityIds(JsonElement cmd)
    {
        var ids = new List<int>();
        if (!cmd.TryGetProperty("entityIds", out var idsElement) ||
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
