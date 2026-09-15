namespace Brickwork.Core.Models;

public static class WallLineEditing
{
    private static readonly WallLineType[] CycleOrder =
    [
        WallLineType.Solid,
        WallLineType.Terrain,
        WallLineType.Invisible,
        WallLineType.Ethereal,
        WallLineType.Door,
        WallLineType.SecretDoor,
        WallLineType.Window,
        WallLineType.Disabled,
    ];

    public static WallLineType CycleType(WallLineType current)
    {
        var index = Array.IndexOf(CycleOrder, current);
        if (index < 0)
        {
            return CycleOrder[0];
        }

        return CycleOrder[(index + 1) % CycleOrder.Length];
    }

    public static void CycleType(Wall wall, WallPortal? portal)
    {
        if (portal is null)
        {
            wall.LineType = CycleType(wall.LineType);
            wall.IsActive = wall.LineType != WallLineType.Disabled;
            return;
        }

        portal.LineType = CycleType(portal.LineType);
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
