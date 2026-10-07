using Brickwork.Core.Geometry;
using Brickwork.Core.Models;

namespace Brickwork.Core.Editing;

/// <summary>
/// In-app clipboard snapshot of walls and/or regions for copy/paste.
/// </summary>
public sealed class MapEntityClipboard
{
    public MapEntityClipboard(IReadOnlyList<Wall> walls, IReadOnlyList<Region> regions)
    {
        Walls = walls;
        Regions = regions;
    }

    public IReadOnlyList<Wall> Walls { get; }

    public IReadOnlyList<Region> Regions { get; }

    public bool IsEmpty => Walls.Count == 0 && Regions.Count == 0;

    public static MapEntityClipboard Empty { get; } = new([], []);

    public static MapEntityClipboard Capture(
        MapDocument map,
        IEnumerable<int> wallEntityIds,
        IEnumerable<int> regionEntityIds)
    {
        var wallIds = wallEntityIds.ToHashSet();
        var regionIds = regionEntityIds.ToHashSet();
        var walls = map.Walls
            .Where(wall => wallIds.Contains(wall.EntityId))
            .Select(wall => CloneWall(wall))
            .ToList();
        var regions = map.Regions
            .Where(region => regionIds.Contains(region.EntityId))
            .Select(region => CloneRegion(region))
            .ToList();
        return new MapEntityClipboard(walls, regions);
    }

    /// <summary>
    /// Pastes clipboard contents into <paramref name="map"/> with a scene-space offset.
    /// Preserves group membership when the source group still exists on the map.
    /// </summary>
    public (IReadOnlyList<Wall> Walls, IReadOnlyList<Region> Regions) PasteInto(
        MapDocument map,
        double offsetX,
        double offsetY)
    {
        if (IsEmpty)
        {
            return ([], []);
        }

        var nextId = RegionConversion.AllocateEntityId(map);
        var pastedWalls = new List<Wall>(Walls.Count);
        var pastedRegions = new List<Region>(Regions.Count);

        foreach (var source in Walls)
        {
            var wall = CloneWall(source, nextId++);
            WallGeometryEditing.Translate(wall, offsetX, offsetY);
            AssignFreshPortalIds(wall);
            wall.GroupId = AttachToGroup(map, wall.EntityId, wall.GroupId);
            map.Walls.Add(wall);
            pastedWalls.Add(wall);
        }

        foreach (var source in Regions)
        {
            var region = CloneRegion(source, nextId++);
            RegionGeometryEditing.Translate(region, offsetX, offsetY);
            region.GroupId = AttachToGroup(map, region.EntityId, region.GroupId);
            map.Regions.Add(region);
            pastedRegions.Add(region);
        }

        return (pastedWalls, pastedRegions);
    }

    /// <summary>
    /// Adds <paramref name="entityId"/> to the group when it exists; otherwise clears membership.
    /// </summary>
    private static int? AttachToGroup(MapDocument map, int entityId, int? groupId)
    {
        if (groupId is not int id)
        {
            return null;
        }

        var group = map.Groups.FirstOrDefault(candidate => candidate.GroupId == id);
        if (group is null)
        {
            return null;
        }

        if (!group.MemberIds.Contains(entityId))
        {
            group.MemberIds.Add(entityId);
        }

        return id;
    }

    private static void AssignFreshPortalIds(Wall wall)
    {
        foreach (var portal in wall.Portals)
        {
            portal.Id = Guid.NewGuid().ToString("N");
        }
    }

    private static Wall CloneWall(Wall wall, int? entityId = null) =>
        new()
        {
            EntityId = entityId ?? wall.EntityId,
            Name = wall.Name,
            LayerId = wall.LayerId,
            IsActive = wall.IsActive,
            IsEntityVisible = wall.IsEntityVisible,
            LineType = wall.LineType,
            WallEnabled = wall.WallEnabled,
            IsClosed = wall.IsClosed,
            PathData = wall.PathData,
            Origin = wall.Origin,
            PathOrigin = wall.PathOrigin,
            RotationPivot = wall.RotationPivot,
            Angle = wall.Angle,
            Scale = wall.Scale,
            WallThickness = wall.WallThickness,
            GroupId = wall.GroupId,
            Points = wall.Points.ToList(),
            Portals = wall.Portals.Select(ClonePortal).ToList(),
        };

    private static Region CloneRegion(Region region, int? entityId = null) =>
        new()
        {
            EntityId = entityId ?? region.EntityId,
            Name = region.Name,
            LayerId = region.LayerId,
            IsActive = region.IsActive,
            IsEntityVisible = region.IsEntityVisible,
            RegionType = region.RegionType,
            GroupId = region.GroupId,
            Points = region.Points.ToList(),
        };

    private static WallPortal ClonePortal(WallPortal portal) =>
        new()
        {
            Id = portal.Id,
            Name = portal.Name,
            Anchor = portal.Anchor,
            Width = portal.Width,
            IsActive = portal.IsActive,
            LineType = portal.LineType,
        };
}
