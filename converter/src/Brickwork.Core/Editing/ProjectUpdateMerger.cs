using System.Text.Json;
using System.Text.Json.Nodes;
using Brickwork.Core.Geometry;
using Brickwork.Core.Models;

namespace Brickwork.Core.Editing;

public sealed record ProjectUpdateResult(MapDocument Map, int Updated, int Added, int Removed);

/// <summary>
/// Updates a project to a newer backup of its source map with a three-way merge: what the user
/// changed in Brickwork (the project compared with an import of the project's own source) is
/// applied to an import of the newer backup. Everything else comes from the newer backup: new
/// walls, regions, lights and layers come in as imported, and ones removed from the backup go.
/// Entities, portals, groups and layers are matched by their Inkarnate ids.
/// </summary>
public static class ProjectUpdateMerger
{
    private const string WallKind = "wall";
    private const string RegionKind = "region";
    private const string LightKind = "light";

    /// <summary>Wall properties that together make up its path; taken from one side as a whole.</summary>
    private static readonly string[] WallGeometry =
    [
        nameof(Wall.IsClosed),
        nameof(Wall.PathData),
        nameof(Wall.Origin),
        nameof(Wall.PathOrigin),
        nameof(Wall.RotationPivot),
        nameof(Wall.Angle),
        nameof(Wall.Scale),
        nameof(Wall.Points),
        nameof(Wall.OffsetBasePoints),
        nameof(Wall.Offset),
    ];

    /// <summary>Properties never taken from the project by the per-property merge.</summary>
    private static readonly HashSet<string> NotMerged =
    [
        .. WallGeometry,
        nameof(Wall.EntityId),
        nameof(Wall.Portals),
        nameof(Wall.Trunk),
        nameof(EntityGroup.GroupId),
        nameof(EntityGroup.MemberIds),
        nameof(MapLayer.Id),
    ];

    /// <param name="project">The edited project map.</param>
    /// <param name="previousImport">The project's source, imported again with the current settings.</param>
    /// <param name="newImport">The newer backup, imported with the same settings; becomes the result.</param>
    public static ProjectUpdateResult Merge(MapDocument project, MapDocument previousImport, MapDocument newImport)
    {
        var mine = Entities(project);
        var original = Entities(previousImport);
        var theirs = Entities(newImport);

        var merged = new List<Entity>();
        var renumbered = new Dictionary<int, int>();
        var reshape = new Dictionary<int, double>();
        int updated = 0, added = 0, removed = 0;

        foreach (var (id, entity) in theirs)
        {
            if (!original.TryGetValue(id, out var before))
            {
                merged.Add(entity);
                added++;
            }
            else if (mine.TryGetValue(id, out var edited))
            {
                merged.Add(MergeEntity(edited, before, entity, reshape));
                updated++;
            }

            // In both imports but not in the project: deleted in Brickwork, so it stays deleted.
        }

        var nextId = new[] { project, previousImport, newImport }
            .SelectMany(AllIds)
            .DefaultIfEmpty(0)
            .Max() + 1;
        foreach (var (id, entity) in mine)
        {
            if (original.ContainsKey(id))
            {
                if (!theirs.ContainsKey(id))
                {
                    removed++;
                }

                continue;
            }

            // Drawn in Brickwork. Brickwork ids follow the highest id in the map, so the newer
            // backup may have used this one for something new.
            if (theirs.ContainsKey(id))
            {
                renumbered[id] = nextId;
                entity.Json[nameof(Wall.EntityId)] = nextId++;
            }

            merged.Add(entity);
        }

        var result = newImport;
        MergeGroups(project, previousImport, result);
        Rebuild(result, merged);
        foreach (var wall in result.Walls)
        {
            if (reshape.TryGetValue(wall.EntityId, out var offset))
            {
                // The path came from the backup: put the project's offset back on it.
                if (Math.Abs(offset) > 1e-9)
                {
                    WallOffset.Apply(wall, offset);
                }
                else
                {
                    WallGeometryEditing.ResnapPortalAnchors(wall);
                }
            }
        }

        RebuildGroupMembers(result, project, previousImport, renumbered);
        MergeLayers(project, previousImport, result);

        result.SceneLevelCount = project.SceneLevelCount;
        result.GlobalIllumination = project.GlobalIllumination;
        result.FoundryExportGrid = project.FoundryExportGrid;
        result.FoundryGridSize = project.FoundryGridSize;
        result.FoundryMapsDirectory = project.FoundryMapsDirectory;
        result.ImportedEntityIds.Clear();
        result.ImportedEntityIds.UnionWith(theirs.Keys);
        return new ProjectUpdateResult(result, updated, added, removed);
    }

    private sealed record Entity(string Kind, JsonObject Json);

    private static Dictionary<int, Entity> Entities(MapDocument map)
    {
        var entities = new Dictionary<int, Entity>();
        foreach (var wall in map.Walls)
        {
            entities.TryAdd(wall.EntityId, new Entity(WallKind, ToJson(wall)));
        }

        foreach (var region in map.Regions)
        {
            entities.TryAdd(region.EntityId, new Entity(RegionKind, ToJson(region)));
        }

        foreach (var light in map.Lights)
        {
            entities.TryAdd(light.EntityId, new Entity(LightKind, ToJson(light)));
        }

        return entities;
    }

    private static IEnumerable<int> AllIds(MapDocument map) =>
        map.Walls.Select(wall => wall.EntityId)
            .Concat(map.Regions.Select(region => region.EntityId))
            .Concat(map.Lights.Select(light => light.EntityId))
            .Concat(map.Groups.Select(group => group.GroupId));

    /// <summary>
    /// The backup's entity with the project's changes on top. A wall's path is taken whole: the
    /// project's when it was edited in Brickwork, else the backup's (with any offset put back).
    /// </summary>
    private static Entity MergeEntity(Entity edited, Entity before, Entity entity, Dictionary<int, double> reshape)
    {
        var result = entity.Json.DeepClone().AsObject();
        ApplyChanges(edited.Json, before.Json, result);

        // A detection change (wall to region) leaves only the shared settings to carry over.
        if (edited.Kind != before.Kind || before.Kind != entity.Kind)
        {
            return entity with { Json = result };
        }

        var pathEdited = false;
        if (entity.Kind == WallKind)
        {
            var offset = edited.Json[nameof(Wall.Offset)]?.GetValue<double>() ?? 0;
            var offsetOnly = Math.Abs(offset) > 1e-9 &&
                             Same(edited.Json, nameof(Wall.OffsetBasePoints), before.Json, nameof(Wall.Points));
            pathEdited = !offsetOnly && !Same(edited.Json, before.Json, nameof(Wall.Points));
            if (pathEdited)
            {
                foreach (var property in WallGeometry)
                {
                    result[property] = edited.Json[property]?.DeepClone();
                }
            }
            else
            {
                reshape[result[nameof(Wall.EntityId)]!.GetValue<int>()] = offsetOnly ? offset : 0;
            }

            result[nameof(Wall.Portals)] = MergePortals(edited.Json, before.Json, entity.Json, pathEdited);
        }
        else if (entity.Kind == RegionKind && !Same(edited.Json, before.Json, nameof(Region.Points)))
        {
            result[nameof(Region.Points)] = edited.Json[nameof(Region.Points)]?.DeepClone();
        }

        return entity with { Json = result };
    }

    /// <summary>
    /// Portals matched by id. Portals added in Brickwork are kept, ones deleted there stay deleted.
    /// Anchors follow the backup unless the project's path is kept.
    /// </summary>
    private static JsonArray MergePortals(JsonObject edited, JsonObject before, JsonObject entity, bool pathEdited)
    {
        var mine = PortalsById(edited);
        var original = PortalsById(before);
        var theirs = PortalsById(entity);
        var result = new JsonArray();
        foreach (var portal in (entity[nameof(Wall.Portals)] as JsonArray ?? []).OfType<JsonObject>())
        {
            var id = PortalId(portal);
            if (id.Length == 0 || !original.TryGetValue(id, out var portalBefore))
            {
                result.Add(portal.DeepClone());
                continue;
            }

            if (!mine.TryGetValue(id, out var portalEdited))
            {
                continue;
            }

            var merged = portal.DeepClone().AsObject();
            ApplyChanges(portalEdited, portalBefore, merged);
            if (!pathEdited)
            {
                merged[nameof(WallPortal.Anchor)] = portal[nameof(WallPortal.Anchor)]?.DeepClone();
            }

            result.Add(merged);
        }

        foreach (var (id, portal) in mine.Where(pair => !original.ContainsKey(pair.Key) && !theirs.ContainsKey(pair.Key)))
        {
            result.Add(portal.DeepClone());
        }

        return result;
    }

    private static Dictionary<string, JsonObject> PortalsById(JsonObject wall)
    {
        var portals = new Dictionary<string, JsonObject>();
        foreach (var portal in (wall[nameof(Wall.Portals)] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (PortalId(portal) is { Length: > 0 } id)
            {
                portals.TryAdd(id, portal);
            }
        }

        return portals;
    }

    private static string PortalId(JsonObject portal) => portal[nameof(WallPortal.Id)]?.GetValue<string>() ?? string.Empty;

    /// <summary>Groups come from the backup, with the project's changes to their settings.</summary>
    private static void MergeGroups(MapDocument project, MapDocument previousImport, MapDocument result)
    {
        var mine = project.Groups.ToDictionary(group => group.GroupId);
        var original = previousImport.Groups.ToDictionary(group => group.GroupId);
        for (var index = 0; index < result.Groups.Count; index++)
        {
            var group = result.Groups[index];
            if (mine.TryGetValue(group.GroupId, out var edited) && original.TryGetValue(group.GroupId, out var before))
            {
                var json = ToJson(group);
                ApplyChanges(ToJson(edited), ToJson(before), json);
                var merged = json.Deserialize<EntityGroup>(BrickworkProject.JsonOptions)!;
                merged.MemberIds.Clear();
                foreach (var memberId in group.MemberIds)
                {
                    merged.MemberIds.Add(memberId);
                }

                result.Groups[index] = merged;
            }
        }
    }

    /// <summary>
    /// Keeps each group's members from the backup, minus ones that left the group, plus members
    /// the project's group had (an import does not list every entity it puts in a group, so
    /// members are not added just because an entity points at the group). Ids that are not walls,
    /// regions, lights or groups (other stamps in an Inkarnate group) stay.
    /// </summary>
    private static void RebuildGroupMembers(MapDocument result, MapDocument project, MapDocument previousImport, Dictionary<int, int> renumbered)
    {
        var groupIds = result.Groups.Select(group => group.GroupId).ToHashSet();
        var parents = new Dictionary<int, int?>();
        foreach (var wall in result.Walls)
        {
            parents[wall.EntityId] = wall.GroupId = Known(wall.GroupId, groupIds);
        }

        foreach (var region in result.Regions)
        {
            parents[region.EntityId] = region.GroupId = Known(region.GroupId, groupIds);
        }

        foreach (var light in result.Lights)
        {
            parents[light.EntityId] = light.GroupId = Known(light.GroupId, groupIds);
        }

        foreach (var group in result.Groups)
        {
            parents[group.GroupId] = group.ParentGroupId = Known(group.ParentGroupId, groupIds);
        }

        var entityIds = new[] { result, project, previousImport }.SelectMany(AllIds).ToHashSet();
        var projectMembers = project.Groups.ToDictionary(
            group => group.GroupId,
            group => group.MemberIds.Select(id => renumbered.GetValueOrDefault(id, id)).ToList());
        foreach (var group in result.Groups)
        {
            var members = group.MemberIds
                .Where(id => parents.TryGetValue(id, out var parent) ? parent == group.GroupId : !entityIds.Contains(id))
                .ToList();
            members.AddRange(projectMembers.GetValueOrDefault(group.GroupId, [])
                .Where(id => parents.TryGetValue(id, out var parent) && parent == group.GroupId && !members.Contains(id)));
            group.MemberIds.Clear();
            foreach (var id in members)
            {
                group.MemberIds.Add(id);
            }
        }
    }

    private static int? Known(int? groupId, HashSet<int> groupIds) =>
        groupId is int id && groupIds.Contains(id) ? id : null;

    /// <summary>
    /// Layers come from the backup, matched by Inkarnate layer id: new ones as imported, removed
    /// ones gone, and names changed in Brickwork kept. Layers made in Brickwork stay while in use.
    /// </summary>
    private static void MergeLayers(MapDocument project, MapDocument previousImport, MapDocument result)
    {
        var mine = project.Layers.ToDictionary(layer => layer.Id, StringComparer.OrdinalIgnoreCase);
        var original = previousImport.Layers.ToDictionary(layer => layer.Id, StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < result.Layers.Count; index++)
        {
            var layer = result.Layers[index];
            if (mine.TryGetValue(layer.Id, out var edited) && original.TryGetValue(layer.Id, out var before))
            {
                var json = ToJson(layer);
                ApplyChanges(ToJson(edited), ToJson(before), json);
                result.Layers[index] = json.Deserialize<MapLayer>(BrickworkProject.JsonOptions)!;
            }
        }

        var inUse = result.Walls.Select(wall => wall.LayerId)
            .Concat(result.Regions.Select(region => region.LayerId))
            .Concat(result.Lights.Select(light => light.LayerId))
            .Concat(result.Groups.Select(group => group.LayerId))
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var order = result.Layers.Count == 0 ? 0 : result.Layers.Max(layer => layer.Order) + 1;
        foreach (var layer in project.Layers.Where(layer =>
                     !original.ContainsKey(layer.Id) &&
                     inUse.Contains(layer.Id) &&
                     result.Layers.All(existing => !string.Equals(existing.Id, layer.Id, StringComparison.OrdinalIgnoreCase))))
        {
            layer.Order = order++;
            result.Layers.Add(layer);
        }
    }

    private static void Rebuild(MapDocument result, List<Entity> merged)
    {
        result.Walls.Clear();
        result.Regions.Clear();
        result.Lights.Clear();
        foreach (var entity in merged)
        {
            switch (entity.Kind)
            {
                case WallKind:
                    result.Walls.Add(entity.Json.Deserialize<Wall>(BrickworkProject.JsonOptions)!);
                    break;
                case RegionKind:
                    result.Regions.Add(entity.Json.Deserialize<Region>(BrickworkProject.JsonOptions)!);
                    break;
                case LightKind:
                    result.Lights.Add(entity.Json.Deserialize<LightSource>(BrickworkProject.JsonOptions)!);
                    break;
            }
        }
    }

    /// <summary>Sets every property the project changed (relative to the original import) on <paramref name="target"/>.</summary>
    private static void ApplyChanges(JsonObject edited, JsonObject before, JsonObject target)
    {
        foreach (var name in target.Select(pair => pair.Key).ToList())
        {
            if (!NotMerged.Contains(name) &&
                edited.ContainsKey(name) &&
                before.ContainsKey(name) &&
                !Same(edited, before, name))
            {
                target[name] = edited[name]?.DeepClone();
            }
        }
    }

    private static bool Same(JsonObject a, JsonObject b, string name) => Same(a, name, b, name);

    private static bool Same(JsonObject a, string nameA, JsonObject b, string nameB) =>
        JsonNode.DeepEquals(a[nameA], b[nameB]);

    private static JsonObject ToJson<T>(T value) =>
        JsonSerializer.SerializeToNode(value, BrickworkProject.JsonOptions)!.AsObject();
}
