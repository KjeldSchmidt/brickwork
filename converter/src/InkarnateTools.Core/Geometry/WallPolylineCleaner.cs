using InkarnateTools.Core.Models;

namespace InkarnateTools.Core.Geometry;

public static class WallPolylineCleaner
{
    /// <summary>
    /// Minimum spacing between consecutive centerline points in scene units.
    /// Tessellation steps at ~1 entity-local unit (= scale scene units).
    /// </summary>
    public const double DefaultMinPointSpacing = 0.5;

    public static List<MapPoint> DeduplicateClosePoints(
        IEnumerable<MapPoint> points,
        double minDistance = DefaultMinPointSpacing,
        bool closeLoop = false)
    {
        var result = new List<MapPoint>();

        foreach (var point in points)
        {
            if (result.Count == 0)
            {
                result.Add(point);
                continue;
            }

            if (Distance(result[^1], point) >= minDistance)
            {
                result.Add(point);
            }
        }

        if (closeLoop && result.Count >= 3 && Distance(result[0], result[^1]) < minDistance)
        {
            result.RemoveAt(result.Count - 1);
        }

        return result;
    }

    public static double MinSpacingForScale(double scale) =>
        Math.Max(DefaultMinPointSpacing, scale * 0.25);

    private static double Distance(MapPoint left, MapPoint right)
    {
        var dx = left.X - right.X;
        var dy = left.Y - right.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
