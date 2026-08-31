using InkarnateTools.Core.Models;

namespace InkarnateTools.Core.Geometry;

public sealed record WallPickTarget(Wall Wall, WallPortal? Portal);

public static class WallHitTester
{
    private const double Epsilon = 1e-6;

    public static WallPickTarget? Pick(
        MapDocument map,
        MapPoint previewPoint,
        double tolerancePreviewPixels)
    {
        var transform = SceneTransform.FromMap(map);
        if (transform is null)
        {
            return null;
        }

        WallPickTarget? bestTarget = null;
        var bestDistanceSquared = tolerancePreviewPixels * tolerancePreviewPixels;

        foreach (var wall in map.Walls)
        {
            if (!wall.WallEnabled || wall.Points.Count < 2)
            {
                continue;
            }

            foreach (var segment in WallPathSegmentBuilder.BuildPortalSegments(wall))
            {
                if (TryPickSegment(segment.Points, transform, previewPoint, ref bestDistanceSquared, out _))
                {
                    bestTarget = new WallPickTarget(wall, segment.Portal);
                }
            }

            foreach (var segment in WallPathSegmentBuilder.BuildSegments(wall))
            {
                if (TryPickSegment(segment, transform, previewPoint, ref bestDistanceSquared, out _))
                {
                    bestTarget = new WallPickTarget(wall, null);
                }
            }
        }

        return bestTarget;
    }

    private static bool TryPickSegment(
        IReadOnlyList<MapPoint> scenePoints,
        SceneTransform transform,
        MapPoint previewPoint,
        ref double bestDistanceSquared,
        out double distanceSquared)
    {
        distanceSquared = double.MaxValue;

        for (var i = 0; i < scenePoints.Count - 1; i++)
        {
            var start = transform.SceneToPreview(scenePoints[i]);
            var end = transform.SceneToPreview(scenePoints[i + 1]);
            var closest = ProjectOntoSegment(previewPoint, start, end, out _);
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

    private static MapPoint ProjectOntoSegment(
        MapPoint point,
        MapPoint start,
        MapPoint end,
        out double t)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = dx * dx + dy * dy;

        if (lengthSquared <= Epsilon)
        {
            t = 0d;
            return start;
        }

        t = Math.Clamp(
            ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared,
            0d,
            1d);

        return new MapPoint(start.X + t * dx, start.Y + t * dy);
    }
}
