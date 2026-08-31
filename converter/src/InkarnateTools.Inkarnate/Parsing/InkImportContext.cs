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

    public Dictionary<string, MapLayer> LayersById { get; } = new(StringComparer.OrdinalIgnoreCase);

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

        Map.Layers.Clear();
        foreach (var layer in LayersById.Values.OrderBy(layer => layer.Order).ThenBy(layer => layer.Id, StringComparer.OrdinalIgnoreCase))
        {
            Map.Layers.Add(layer);
        }
    }

    public MapLayer EnsureLayer(string layerId)
    {
        if (LayersById.TryGetValue(layerId, out var existing))
        {
            return existing;
        }

        var layer = new MapLayer
        {
            Id = layerId,
            Order = LayersById.Count,
        };
        LayersById[layerId] = layer;
        return layer;
    }

    public void ReorderLayers(IReadOnlyList<string> newOrder)
    {
        for (var index = 0; index < newOrder.Count; index++)
        {
            var layer = EnsureLayer(newOrder[index]);
            layer.Order = index;
        }

        var nextOrder = newOrder.Count;
        foreach (var layer in LayersById.Values
                     .Where(layer => !newOrder.Contains(layer.Id, StringComparer.OrdinalIgnoreCase))
                     .OrderBy(layer => layer.Order)
                     .ThenBy(layer => layer.Id, StringComparer.OrdinalIgnoreCase)
                     .ToList())
        {
            layer.Order = nextOrder++;
        }
    }

    public void SetLayerVisibility(string layerId, bool isVisible)
    {
        var layer = EnsureLayer(layerId);
        layer.IsVisible = isVisible;
        ApplyLayerVisibilityToEntities(layerId, isVisible);
    }

    public void MoveEntityToLayer(int entityId, string targetLayerId)
    {
        if (WallsByEntityId.TryGetValue(entityId, out var wall))
        {
            wall.LayerId = targetLayerId;
            ApplyVisibilityToWall(wall, targetLayerId);
            return;
        }

        if (!GroupsById.TryGetValue(entityId, out var group))
        {
            return;
        }

        group.LayerId = targetLayerId;
        foreach (var descendant in EnumerateDescendantWalls(group))
        {
            descendant.LayerId = targetLayerId;
            ApplyVisibilityToWall(descendant, targetLayerId);
        }

        foreach (var nested in EnumerateDescendantGroups(group))
        {
            nested.LayerId = targetLayerId;
        }
    }

    public void ApplyVisibilityToWall(Wall wall, string? layerId)
    {
        if (layerId is null ||
            !LayersById.TryGetValue(layerId, out var layer))
        {
            return;
        }

        wall.IsActive = layer.IsVisible;
        foreach (var portal in wall.Portals)
        {
            portal.IsActive = layer.IsVisible;
        }
    }

    private void ApplyLayerVisibilityToEntities(string layerId, bool isVisible)
    {
        foreach (var wall in WallsByEntityId.Values.Where(wall =>
                     string.Equals(wall.LayerId, layerId, StringComparison.OrdinalIgnoreCase)))
        {
            wall.IsActive = isVisible;
            foreach (var portal in wall.Portals)
            {
                portal.IsActive = isVisible;
            }
        }
    }

    private IEnumerable<EntityGroup> EnumerateDescendantGroups(EntityGroup group)
    {
        foreach (var memberId in group.MemberIds)
        {
            if (!GroupsById.TryGetValue(memberId, out var childGroup))
            {
                continue;
            }

            yield return childGroup;
            foreach (var nested in EnumerateDescendantGroups(childGroup))
            {
                yield return nested;
            }
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
