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

    public Dictionary<int, Wall> WallsByEntityId { get; } = [];

    public Dictionary<int, EntityGroup> GroupsById { get; } = [];

    public void SyncWalls()
    {
        Map.Walls.Clear();
        foreach (var wall in WallsByEntityId.Values.OrderBy(w => w.EntityId))
        {
            Map.Walls.Add(wall);
        }

        Map.Groups.Clear();
        foreach (var group in GroupsById.Values.OrderBy(g => g.GroupId))
        {
            Map.Groups.Add(group);
        }
    }

    public void DetachFromParent(int entityId)
    {
        if (WallsByEntityId.TryGetValue(entityId, out var wall))
        {
            if (wall.GroupId is int parentId &&
                GroupsById.TryGetValue(parentId, out var parentGroup))
            {
                parentGroup.MemberIds.Remove(entityId);
            }

            wall.GroupId = null;
            return;
        }

        if (GroupsById.TryGetValue(entityId, out var group))
        {
            if (group.ParentGroupId is int parentId &&
                GroupsById.TryGetValue(parentId, out var parentGroup))
            {
                parentGroup.MemberIds.Remove(entityId);
            }

            group.ParentGroupId = null;
        }
    }

    public void AttachToGroup(int entityId, int groupId)
    {
        if (entityId == groupId)
        {
            return;
        }

        if (!GroupsById.TryGetValue(groupId, out var group))
        {
            group = new EntityGroup { GroupId = groupId };
            GroupsById[groupId] = group;
        }

        DetachFromParent(entityId);

        if (WallsByEntityId.TryGetValue(entityId, out var wall))
        {
            group.LayerId ??= wall.LayerId;
            wall.GroupId = groupId;
            if (!group.MemberIds.Contains(entityId))
            {
                group.MemberIds.Add(entityId);
            }

            return;
        }

        if (GroupsById.TryGetValue(entityId, out var childGroup))
        {
            group.LayerId ??= childGroup.LayerId;
            childGroup.ParentGroupId = groupId;
            if (!group.MemberIds.Contains(entityId))
            {
                group.MemberIds.Add(entityId);
            }
        }
    }

    public bool RemoveEntity(int entityId) => RemoveEntity(entityId, new HashSet<int>());

    private bool RemoveEntity(int entityId, HashSet<int> visiting)
    {
        if (!visiting.Add(entityId))
        {
            return false;
        }

        var removed = false;

        if (WallsByEntityId.Remove(entityId))
        {
            removed = true;
            foreach (var group in GroupsById.Values)
            {
                group.MemberIds.Remove(entityId);
            }
        }

        if (GroupsById.Remove(entityId, out var removedGroup))
        {
            removed = true;

            if (removedGroup.ParentGroupId is int parentId &&
                GroupsById.TryGetValue(parentId, out var parentGroup))
            {
                parentGroup.MemberIds.Remove(entityId);
            }

            // Deleting a group deletes its members (Inkarnate semantics).
            foreach (var memberId in removedGroup.MemberIds.ToList())
            {
                RemoveEntity(memberId, visiting);
            }
        }

        return removed;
    }

    public IEnumerable<Wall> EnumerateDescendantWalls(EntityGroup group)
    {
        foreach (var memberId in group.MemberIds)
        {
            if (WallsByEntityId.TryGetValue(memberId, out var wall))
            {
                yield return wall;
            }
            else if (GroupsById.TryGetValue(memberId, out var childGroup))
            {
                foreach (var descendant in EnumerateDescendantWalls(childGroup))
                {
                    yield return descendant;
                }
            }
        }
    }
}
