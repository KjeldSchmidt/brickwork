namespace Brickwork.Core.Models;

public static class RegionEditing
{
    private static readonly RegionType[] CycleOrder =
    [
        RegionType.DifficultTerrain,
        RegionType.Other,
    ];

    public static RegionType CycleType(RegionType current)
    {
        var index = Array.IndexOf(CycleOrder, current);
        if (index < 0)
        {
            return CycleOrder[0];
        }

        return CycleOrder[(index + 1) % CycleOrder.Length];
    }

    public static void CycleType(Region region) => region.RegionType = CycleType(region.RegionType);

    public static void SetRegionType(Region region, RegionType regionType) =>
        region.RegionType = regionType;

    public static bool RemoveFromMap(MapDocument map, Region region)
    {
        if (!map.Regions.Remove(region))
        {
            var match = map.Regions.FirstOrDefault(candidate => candidate.EntityId == region.EntityId);
            if (match is null || !map.Regions.Remove(match))
            {
                return false;
            }
        }

        foreach (var group in map.Groups)
        {
            group.MemberIds.Remove(region.EntityId);
        }

        return true;
    }
}
