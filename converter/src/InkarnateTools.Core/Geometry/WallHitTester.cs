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
                if (TryPickSegment(segment.Points, isClosed: false, transform, previewPoint, ref bestDistanceSquared, out _))
                {
                    bestTarget = new WallPickTarget(wall, segment.Portal);
                }
            }

            foreach (var segment in WallPathSegmentBuilder.BuildSegments(wall))
            {
                if (TryPickSegment(segment.Points, segment.IsClosed, transform, previewPoint, ref bestDistanceSquared, out _))
                {
                    bestTarget = new WallPickTarget(wall, null);
                }
            }
        }

        return bestTarget;
    }

    private static bool TryPickSegment(
        IReadOnlyList<MapPoint> scenePoints,
        bool isClosed,
        SceneTransform transform,
        MapPoint previewPoint,
        ref double bestDistanceSquared,
        out double distanceSquared)
    {
        distanceSquared = double.MaxValue;
        var edgeCount = WallPolylineEdges.EdgeCount(scenePoints.Count, isClosed);

        for (var i = 0; i < edgeCount; i++)
        {
            var start = scenePoints[i];
            var end = scenePoints[(i + 1) % scenePoints.Count];
            var previewStart = transform.SceneToPreview(start);
            var previewEnd = transform.SceneToPreview(end);
            var closest = ProjectOntoSegment(previewPoint, previewStart, previewEnd, out _);
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
