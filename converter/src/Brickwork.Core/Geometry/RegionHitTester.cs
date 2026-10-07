using Brickwork.Core.Models;

namespace Brickwork.Core.Geometry;

public sealed record RegionPickTarget(Region Region);

public static class RegionHitTester
{
    public static RegionPickTarget? Pick(
        MapDocument map,
        MapPoint previewPoint,
        double tolerancePreviewPixels)
    {
        var transform = SceneTransform.FromMap(map);
        if (transform is null)
        {
            return null;
        }

        RegionPickTarget? bestTarget = null;
        var bestDistanceSquared = tolerancePreviewPixels * tolerancePreviewPixels;

        foreach (var region in map.Regions)
        {
            if (!region.IsActive || region.Points.Count < 3)
            {
                continue;
            }

            if (TryPickClosedPolyline(
                    region.Points,
                    transform,
                    previewPoint,
                    ref bestDistanceSquared))
            {
                bestTarget = new RegionPickTarget(region);
            }
        }

        return bestTarget;
    }

    private static bool TryPickClosedPolyline(
        IList<MapPoint> scenePoints,
        SceneTransform transform,
        MapPoint previewPoint,
        ref double bestDistanceSquared)
    {
        var count = scenePoints.Count;
        var edgeCount = WallPolylineEdges.EdgeCount(count, isClosed: true);
        var distanceSquared = double.MaxValue;

        for (var i = 0; i < edgeCount; i++)
        {
            var start = scenePoints[i];
            var end = scenePoints[(i + 1) % count];
            var previewStart = transform.SceneToPreview(start);
            var previewEnd = transform.SceneToPreview(end);
            var closest = ProjectOntoSegment(previewPoint, previewStart, previewEnd);
            var dx = previewPoint.X - closest.X;
            var dy = previewPoint.Y - closest.Y;
            var segmentDistanceSquared = dx * dx + dy * dy;
            if (segmentDistanceSquared < distanceSquared)
            {
                distanceSquared = segmentDistanceSquared;
            }
        }

        if (distanceSquared > bestDistanceSquared)
        {
            return false;
        }

        bestDistanceSquared = distanceSquared;
        return true;
    }

    private static MapPoint ProjectOntoSegment(MapPoint point, MapPoint start, MapPoint end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = (dx * dx) + (dy * dy);
        if (lengthSquared < 1e-12)
        {
            return start;
        }

        var t = (((point.X - start.X) * dx) + ((point.Y - start.Y) * dy)) / lengthSquared;
        t = Math.Clamp(t, 0d, 1d);
        return new MapPoint(start.X + (dx * t), start.Y + (dy * t));
    }
}
