namespace InkarnateTools.Core.Models;

public static class WallLineEditing
{
    private static readonly WallLineType[] CycleOrder = Enum.GetValues<WallLineType>();

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
