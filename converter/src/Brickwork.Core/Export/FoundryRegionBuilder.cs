using Brickwork.Core.Models;

namespace Brickwork.Core.Export;

public sealed record FoundryRegionPolygon(IReadOnlyList<int> Points);

public sealed record FoundryRegion(
    string Name,
    string Color,
    IReadOnlyList<FoundryRegionPolygon> Shapes,
    int? WalkMovementCost);

public static class FoundryRegionBuilder
{
    public static IReadOnlyList<FoundryRegion> BuildFromMap(MapDocument map)
    {
        var transform = FoundryExportCanvas.FromMap(map).Transform;
        var regions = new List<FoundryRegion>();

        foreach (var regionType in Enum.GetValues<RegionType>())
        {
            var shapes = map.Regions
                .Where(region => region.IsActive && region.RegionType == regionType)
                .Select(region => TryBuildPolygon(region, transform))
                .Where(shape => shape is not null)
                .Select(shape => shape!)
                .ToList();

            if (shapes.Count == 0)
            {
                continue;
            }

            regions.Add(new FoundryRegion(
                regionType.ToDisplayName(),
                ColorFor(regionType),
                shapes,
                WalkMovementCostFor(regionType)));
        }

        return regions;
    }

    private static FoundryRegionPolygon? TryBuildPolygon(Region region, SceneTransform transform)
    {
        if (region.Points.Count < 3)
        {
            return null;
        }

        var points = new List<int>(region.Points.Count * 2);
        int? lastX = null;
        int? lastY = null;

        foreach (var scenePoint in region.Points)
        {
            var preview = transform.SceneToPreview(scenePoint);
            var x = (int)Math.Round(preview.X);
            var y = (int)Math.Round(preview.Y);

            if (lastX == x && lastY == y)
            {
                continue;
            }

            points.Add(x);
            points.Add(y);
            lastX = x;
            lastY = y;
        }

        // Drop closing duplicate if present.
        if (points.Count >= 6 &&
            points[0] == points[^2] &&
            points[1] == points[^1])
        {
            points.RemoveRange(points.Count - 2, 2);
        }

        return points.Count >= 6 ? new FoundryRegionPolygon(points) : null;
    }

    private static string ColorFor(RegionType regionType) =>
        regionType switch
        {
            RegionType.DifficultTerrain => "#cc7428",
            RegionType.GreaterDifficultTerrain => "#6d28cc",
            _ => "#1aa3a3",
        };

    private static int? WalkMovementCostFor(RegionType regionType) =>
        regionType switch
        {
            RegionType.DifficultTerrain => 2,
            RegionType.GreaterDifficultTerrain => 3,
            _ => null,
        };
}
