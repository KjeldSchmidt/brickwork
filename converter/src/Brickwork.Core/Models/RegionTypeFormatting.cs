namespace Brickwork.Core.Models;

public static class RegionTypeFormatting
{
    public static string ToDisplayName(this RegionType regionType) =>
        regionType switch
        {
            RegionType.DifficultTerrain => "Difficult Terrain",
            RegionType.GreaterDifficultTerrain => "Greater Difficult Terrain",
            RegionType.Other => "Other",
            _ => regionType.ToString(),
        };
}
