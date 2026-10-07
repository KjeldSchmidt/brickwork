using Brickwork.Core.Geometry;

namespace Brickwork.Core.Models;

public static class RegionConversion
{
    public static Region WallToRegion(Wall wall, int entityId)
    {
        var points = wall.IsClosed
            ? wall.Points.ToList()
            : BuildOpenWallBoundary(wall).ToList();

        return new Region
        {
            EntityId = entityId,
            Name = wall.Name,
            LayerId = wall.LayerId,
            IsActive = wall.IsActive,
            IsEntityVisible = wall.IsEntityVisible,
            GroupId = wall.GroupId,
            RegionType = RegionType.DifficultTerrain,
            Points = points,
        };
    }

    public static Wall RegionToWall(Region region, int entityId) =>
        new()
        {
            EntityId = entityId,
            Name = region.Name,
            LayerId = region.LayerId,
            IsActive = region.IsActive,
            IsEntityVisible = region.IsEntityVisible,
            GroupId = region.GroupId,
            LineType = WallLineType.Solid,
            WallEnabled = true,
            IsClosed = true,
            Scale = 1,
            Points = region.Points.ToList(),
        };

    public static bool TryConvertWallToRegion(MapDocument map, Wall wall, out Region? region)
    {
        region = null;
        if (!map.Walls.Contains(wall) &&
            map.Walls.All(candidate => candidate.EntityId != wall.EntityId))
        {
            return false;
        }

        var entityId = AllocateEntityId(map);
        region = WallToRegion(wall, entityId);
        if (region.Points.Count < 3)
        {
            region = null;
            return false;
        }

        if (!WallLineEditing.RemoveFromMap(map, wall))
        {
            region = null;
            return false;
        }

        map.Regions.Add(region);
        if (region.GroupId is int groupId)
        {
            var group = map.Groups.FirstOrDefault(candidate => candidate.GroupId == groupId);
            group?.MemberIds.Add(region.EntityId);
        }

        return true;
    }

    public static bool TryConvertRegionToWall(MapDocument map, Region region, out Wall? wall)
    {
        wall = null;
        if (!map.Regions.Contains(region) &&
            map.Regions.All(candidate => candidate.EntityId != region.EntityId))
        {
            return false;
        }

        if (region.Points.Count < 3)
        {
            return false;
        }

        var entityId = AllocateEntityId(map);
        wall = RegionToWall(region, entityId);

        if (!RegionEditing.RemoveFromMap(map, region))
        {
            wall = null;
            return false;
        }

        map.Walls.Add(wall);
        if (wall.GroupId is int groupId)
        {
            var group = map.Groups.FirstOrDefault(candidate => candidate.GroupId == groupId);
            group?.MemberIds.Add(wall.EntityId);
        }

        return true;
    }

    public static int AllocateEntityId(MapDocument map)
    {
        var maxWall = map.Walls.Count == 0 ? 0 : map.Walls.Max(wall => wall.EntityId);
        var maxRegion = map.Regions.Count == 0 ? 0 : map.Regions.Max(region => region.EntityId);
        return Math.Max(maxWall, maxRegion) + 1;
    }

    private static IReadOnlyList<MapPoint> BuildOpenWallBoundary(Wall wall)
    {
        var thickness = wall.SceneThickness;
        if (thickness <= 1e-9)
        {
            thickness = WallLineEditing.DefaultTerrainWallThickness;
        }

        return WallThicknessPolygonBuilder.BuildOutline(wall.Points, thickness, isClosed: false);
    }
}
