namespace Brickwork.Core.Models;

public static class WallLineEditing
{
    public const double DefaultTerrainWallThickness = 50d;

    /// <summary>Canvas cycle for walls — doors stay panel-only.</summary>
    private static readonly WallLineType[] WallCycleOrder =
    [
        WallLineType.Solid,
        WallLineType.Terrain,
        WallLineType.Invisible,
        WallLineType.Ethereal,
        WallLineType.Window,
        WallLineType.Disabled,
    ];

    /// <summary>Canvas cycle for portals — door-like types only.</summary>
    private static readonly WallLineType[] PortalCycleOrder =
    [
        WallLineType.Door,
        WallLineType.SecretDoor,
        WallLineType.Window,
        WallLineType.Disabled,
    ];

    public static WallLineType CycleType(WallLineType current) => CycleWallType(current);

    public static WallLineType CycleWallType(WallLineType current) =>
        CycleIn(WallCycleOrder, current);

    public static WallLineType CyclePortalType(WallLineType current) =>
        CycleIn(PortalCycleOrder, current);

    public static void CycleType(Wall wall, WallPortal? portal)
    {
        if (portal is null)
        {
            SetLineType(wall, CycleWallType(wall.LineType));
            return;
        }

        portal.LineType = CyclePortalType(portal.LineType);
    }

    private static WallLineType CycleIn(WallLineType[] order, WallLineType current)
    {
        var index = Array.IndexOf(order, current);
        if (index < 0)
        {
            return order[0];
        }

        return order[(index + 1) % order.Length];
    }

    public static void SetLineType(Wall wall, WallLineType lineType)
    {
        wall.LineType = lineType;
        wall.IsActive = lineType != WallLineType.Disabled;
        EnsureDefaultTerrainThickness(wall);
    }

    /// <summary>
    /// Seeds a usable thickness when a wall first becomes Terrain with no thickness set.
    /// Does not overwrite imported or previously edited non-zero values.
    /// </summary>
    public static void EnsureDefaultTerrainThickness(Wall wall)
    {
        if (wall.LineType == WallLineType.Terrain && wall.WallThickness <= 0)
        {
            wall.WallThickness = DefaultTerrainWallThickness;
        }
    }

    public static void SetWallEnabled(Wall wall, bool enabled)
    {
        if (enabled)
        {
            wall.IsActive = true;
            if (wall.LineType == WallLineType.Disabled)
            {
                wall.LineType = WallLineType.Solid;
            }

            return;
        }

        wall.IsActive = false;
        wall.LineType = WallLineType.Disabled;
    }

    public static void ToggleActive(Wall wall, WallPortal? portal)
    {
        if (portal is null)
        {
            SetWallEnabled(wall, !wall.IsActive || wall.LineType == WallLineType.Disabled);
            return;
        }

        portal.IsActive = !portal.IsActive;
    }

    public static bool RemoveFromMap(MapDocument map, Wall wall)
    {
        if (!map.Walls.Remove(wall))
        {
            var match = map.Walls.FirstOrDefault(candidate => candidate.EntityId == wall.EntityId);
            if (match is null || !map.Walls.Remove(match))
            {
                return false;
            }
        }

        foreach (var group in map.Groups)
        {
            group.MemberIds.Remove(wall.EntityId);
        }

        return true;
    }
}
