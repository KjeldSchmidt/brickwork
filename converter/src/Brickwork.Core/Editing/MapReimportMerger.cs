using Brickwork.Core.Geometry;
using Brickwork.Core.Models;

namespace Brickwork.Core.Editing;

public sealed record MapReimportResult(int Updated, int Added, int Removed);

/// <summary>
/// Merges a fresh import of the same source file (made with changed import settings) into an
/// edited map. Geometry and detection come from the fresh import; the user's edits are kept:
/// names, types, on/off states, offsets and light settings of existing entities, entities the
/// user drew, and entities the user deleted (they are not brought back).
/// </summary>
public static class MapReimportMerger
{
    public static void RecordImportedIds(MapDocument map)
    {
        map.ImportedEntityIds.Clear();
        map.ImportedEntityIds.UnionWith(map.Walls.Select(wall => wall.EntityId));
        map.ImportedEntityIds.UnionWith(map.Regions.Select(region => region.EntityId));
        map.ImportedEntityIds.UnionWith(map.Lights.Select(light => light.EntityId));
    }

    public static MapReimportResult Merge(
        MapDocument target,
        MapDocument fresh,
        bool wallsAndRegions,
        bool lights)
    {
        var result = new MapReimportResult(0, 0, 0);
        if (wallsAndRegions)
        {
            result = Add(result, MergeWallsAndRegions(target, fresh));
        }

        if (lights)
        {
            result = Add(result, MergeLights(target, fresh));
        }

        return result;
    }

    private static MapReimportResult MergeWallsAndRegions(MapDocument target, MapDocument fresh)
    {
        var imported = target.ImportedEntityIds;
        var freshWalls = fresh.Walls.ToDictionary(wall => wall.EntityId);
        var freshRegions = fresh.Regions.ToDictionary(region => region.EntityId);
        int updated = 0, added = 0, removed = 0;

        var oldRegions = target.Regions.ToList();
        foreach (var wall in target.Walls.ToList())
        {
            if (freshWalls.TryGetValue(wall.EntityId, out var freshWall))
            {
                UpdateWall(wall, freshWall);
                updated++;
            }
            else if (freshRegions.TryGetValue(wall.EntityId, out var freshRegion))
            {
                // Region detection now claims this path.
                target.Walls.Remove(wall);
                CarryOver(wall, freshRegion);
                target.Regions.Add(freshRegion);
                updated++;
            }
            else if (imported.Contains(wall.EntityId))
            {
                target.Walls.Remove(wall);
                RemoveFromGroups(target, wall.EntityId);
                removed++;
            }
        }

        foreach (var region in oldRegions)
        {
            if (freshRegions.TryGetValue(region.EntityId, out var freshRegion))
            {
                Replace(region.Points, freshRegion.Points);
                updated++;
            }
            else if (freshWalls.TryGetValue(region.EntityId, out var freshWall))
            {
                // Region detection no longer claims this path: it is a wall again.
                target.Regions.Remove(region);
                freshWall.Name = region.Name;
                freshWall.LayerId = region.LayerId;
                freshWall.GroupId = region.GroupId;
                target.Walls.Add(freshWall);
                updated++;
            }
            else if (imported.Contains(region.EntityId))
            {
                target.Regions.Remove(region);
                RemoveFromGroups(target, region.EntityId);
                removed++;
            }
        }

        // Groups the fresh import created (e.g. tree trunk groups after enabling tree detection).
        foreach (var freshGroup in fresh.Groups.Where(group => target.Groups.All(existing => existing.GroupId != group.GroupId)))
        {
            freshGroup.MemberIds.Clear();
            target.Groups.Add(freshGroup);
        }

        var present = target.Walls.Select(wall => wall.EntityId)
            .Concat(target.Regions.Select(region => region.EntityId))
            .ToHashSet();
        foreach (var freshWall in fresh.Walls.Where(wall => IsNew(wall.EntityId, present, imported)))
        {
            target.Walls.Add(freshWall);
            AddToGroup(target, freshWall.EntityId, freshWall.GroupId);
            added++;
        }

        foreach (var freshRegion in fresh.Regions.Where(region => IsNew(region.EntityId, present, imported)))
        {
            target.Regions.Add(freshRegion);
            AddToGroup(target, freshRegion.EntityId, freshRegion.GroupId);
            added++;
        }

        imported.UnionWith(freshWalls.Keys);
        imported.UnionWith(freshRegions.Keys);
        return new MapReimportResult(updated, added, removed);
    }

    private static MapReimportResult MergeLights(MapDocument target, MapDocument fresh)
    {
        var imported = target.ImportedEntityIds;
        var freshLights = fresh.Lights.ToDictionary(light => light.EntityId);
        int updated = 0, added = 0, removed = 0;

        foreach (var light in target.Lights.ToList())
        {
            if (freshLights.TryGetValue(light.EntityId, out var freshLight))
            {
                light.Position = freshLight.Position;
                light.SourceName = freshLight.SourceName;
                updated++;
            }
            else if (imported.Contains(light.EntityId))
            {
                target.Lights.Remove(light);
                RemoveFromGroups(target, light.EntityId);
                removed++;
            }
        }

        var present = target.Lights.Select(light => light.EntityId).ToHashSet();
        foreach (var freshLight in fresh.Lights.Where(light => IsNew(light.EntityId, present, imported)))
        {
            target.Lights.Add(freshLight);
            AddToGroup(target, freshLight.EntityId, freshLight.GroupId);
            added++;
        }

        imported.UnionWith(freshLights.Keys);
        return new MapReimportResult(updated, added, removed);
    }

    /// <summary>Takes fresh geometry and portals; keeps the user's settings and re-applies the offset.</summary>
    private static void UpdateWall(Wall wall, Wall fresh)
    {
        var offset = wall.Offset;
        var oldPortals = wall.Portals
            .Where(portal => !string.IsNullOrEmpty(portal.Id))
            .GroupBy(portal => portal.Id)
            .ToDictionary(group => group.Key, group => group.First());

        wall.IsClosed = fresh.IsClosed;
        wall.PathData = fresh.PathData;
        wall.Origin = fresh.Origin;
        wall.PathOrigin = fresh.PathOrigin;
        wall.RotationPivot = fresh.RotationPivot;
        wall.Angle = fresh.Angle;
        wall.Scale = fresh.Scale;
        Replace(wall.Points, fresh.Points);
        wall.OffsetBasePoints.Clear();
        wall.Offset = 0;

        wall.Portals.Clear();
        foreach (var portal in fresh.Portals)
        {
            if (oldPortals.TryGetValue(portal.Id, out var old))
            {
                portal.Name = old.Name;
                portal.Width = old.Width;
                portal.IsActive = old.IsActive;
                portal.LineType = old.LineType;
            }

            wall.Portals.Add(portal);
        }

        // Trunk size and adjustments come from the fresh import (which reads the tree-trunk library).
        wall.Trunk = fresh.Trunk;

        if (Math.Abs(offset) > 1e-9)
        {
            WallOffset.Apply(wall, offset);
        }
        else
        {
            WallGeometryEditing.ResnapPortalAnchors(wall);
        }
    }

    private static void CarryOver(Wall wall, Region region)
    {
        region.Name = wall.Name;
        region.LayerId = wall.LayerId;
        region.GroupId = wall.GroupId;
        region.IsActive = wall.IsActive;
    }

    private static bool IsNew(int entityId, HashSet<int> present, ISet<int> imported) =>
        !present.Contains(entityId) && !imported.Contains(entityId);

    private static void AddToGroup(MapDocument map, int entityId, int? groupId)
    {
        if (groupId is int id &&
            map.Groups.FirstOrDefault(group => group.GroupId == id) is { } group &&
            !group.MemberIds.Contains(entityId))
        {
            group.MemberIds.Add(entityId);
        }
    }

    private static void RemoveFromGroups(MapDocument map, int entityId)
    {
        foreach (var group in map.Groups)
        {
            group.MemberIds.Remove(entityId);
        }
    }

    private static void Replace(IList<MapPoint> target, IEnumerable<MapPoint> source)
    {
        var points = source.ToList();
        target.Clear();
        foreach (var point in points)
        {
            target.Add(point);
        }
    }

    private static MapReimportResult Add(MapReimportResult a, MapReimportResult b) =>
        new(a.Updated + b.Updated, a.Added + b.Added, a.Removed + b.Removed);
}
