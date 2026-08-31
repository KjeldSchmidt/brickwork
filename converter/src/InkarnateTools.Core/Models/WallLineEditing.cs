namespace InkarnateTools.Core.Models;

public static class WallLineEditing
{
    public static WallLineType CycleType(WallLineType current) =>
        current switch
        {
            WallLineType.Default => WallLineType.Door,
            WallLineType.Door => WallLineType.Terrain,
            _ => WallLineType.Default,
        };

    public static void CycleType(Wall wall, WallPortal? portal)
    {
        if (portal is null)
        {
            wall.LineType = CycleType(wall.LineType);
            return;
        }

        portal.LineType = CycleType(portal.LineType);
    }

    public static void ToggleActive(Wall wall, WallPortal? portal)
    {
        if (portal is null)
        {
            wall.IsActive = !wall.IsActive;
            return;
        }

        portal.IsActive = !portal.IsActive;
    }
}
