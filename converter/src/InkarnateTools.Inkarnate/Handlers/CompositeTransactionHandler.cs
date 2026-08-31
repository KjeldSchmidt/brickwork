using System.Text.Json;
using InkarnateTools.Core.Models;
using InkarnateTools.Inkarnate.Parsing;

namespace InkarnateTools.Inkarnate.Handlers;

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

        var understanding = TransactionUnderstanding.FullyUnderstood;
        var commandCount = 0;

        foreach (var cmd in cmdsElement.EnumerateArray())
        {
            commandCount++;
            var cmdType = InkJsonReader.ReadString(cmd, "cmdType");
            understanding = Max(understanding, cmdType switch
            {
                "cmd-entity-group" => ApplyGroup(context, cmd),
                "cmd-entity-move-to-group" => ApplyMoveToGroup(context, cmd),
                _ => _processNested(context, cmd).Understanding,
            });
        }

        if (commandCount == 0)
        {
            return TransactionAnalysisFactory.Create(
                transaction,
                CommandType,
                TransactionUnderstanding.Unknown,
                "empty cmds");
        }

        return TransactionAnalysisFactory.Create(transaction, CommandType, understanding);
    }

    private static TransactionUnderstanding ApplyGroup(InkImportContext context, JsonElement cmd)
    {
        var groupId = InkJsonReader.ReadInt(cmd, "groupId");
        if (groupId is null or <= 0)
        {
            return TransactionUnderstanding.Unknown;
        }

        var memberIds = ReadEntityIds(cmd);
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
            // Detach without relying on MemberIds membership list (cleared above).
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

        return TransactionUnderstanding.FullyUnderstood;
    }

    private static TransactionUnderstanding ApplyMoveToGroup(InkImportContext context, JsonElement cmd)
    {
        var memberIds = ReadEntityIds(cmd);
        if (memberIds.Count == 0)
        {
            return TransactionUnderstanding.KnownIgnored;
        }

        var groupId = InkJsonReader.ReadInt(cmd, "groupId");
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

        return TransactionUnderstanding.FullyUnderstood;
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

    private static TransactionUnderstanding Max(
        TransactionUnderstanding current,
        TransactionUnderstanding candidate) =>
        (TransactionUnderstanding)Math.Max((int)current, (int)candidate);
}
