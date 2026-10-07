namespace Brickwork.Core.Models;

public static class RegionTypeFormatting
{
    public static string ToDisplayName(this RegionType regionType) =>
        regionType switch
        {
            RegionType.DifficultTerrain => "Difficult Terrain",
            _ => regionType.ToString(),
        };
}
