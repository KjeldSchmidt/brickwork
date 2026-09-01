using InkarnateTools.Core.Models;

namespace InkarnateTools.Core.Geometry;

public static class WallGeometryEditing
{
    private const double Epsilon = 1e-6;

    public static void SetVertexPosition(Wall wall, int vertexIndex, MapPoint scenePoint)
    {
        if (vertexIndex < 0 || vertexIndex >= wall.Points.Count)
        {
            return;
        }

        wall.Points[vertexIndex] = scenePoint;
    }

    public static void SetPortalAnchorFromScene(Wall wall, WallPortal portal, MapPoint scenePoint)
    {
        var snappedScene = SnapToCenterline(wall, scenePoint);
        portal.Anchor = MapPointTransforms.SceneToLocal(wall, snappedScene);
    }

    public static void SetPortalEndpointFromScene(
        Wall wall,
        WallPortal portal,
        PortalWidthEndpoint endpoint,
        MapPoint scenePoint)
    {
        if (!WallPathSegmentBuilder.TryGetPortalArcInterval(wall, portal, out var currentStart, out var currentEnd))
        {
            return;
        }

        var totalLength = WallPolylineEdges.TotalLength(wall.Points, wall.IsClosed);
        var arcLengths = WallPolylineEdges.ComputeArcLengths(wall.Points, wall.IsClosed);
        var snappedScene = SnapToCenterline(wall, scenePoint);
        var draggedArc = FindArcLengthAtClosestPoint(wall.Points, wall.IsClosed, arcLengths, snappedScene);

        var center = (currentStart + currentEnd) / 2d;
        var halfWidth = Math.Abs(draggedArc - center);

        const double minWidth = 2d;
        halfWidth = Math.Max(halfWidth, minWidth / 2d);
        halfWidth = Math.Min(halfWidth, center);
        halfWidth = Math.Min(halfWidth, totalLength - center);

        portal.Width = halfWidth * 2d;
    }

    public static MapPoint SnapToCenterline(Wall wall, MapPoint scenePoint)
    {
        if (wall.Points.Count < 2)
        {
            return scenePoint;
        }

        var arcLengths = WallPolylineEdges.ComputeArcLengths(wall.Points, wall.IsClosed);
        var arcLength = FindArcLengthAtClosestPoint(wall.Points, wall.IsClosed, arcLengths, scenePoint);
        return InterpolateAtLength(wall.Points, wall.IsClosed, arcLength);
    }

    private static double FindArcLengthAtClosestPoint(
        IList<MapPoint> points,
        bool isClosed,
        double[] arcLengths,
        MapPoint target)
    {
        var bestDistanceSquared = double.MaxValue;
        var bestArcLength = 0d;

        foreach (var (start, end, startIndex) in WallPolylineEdges.EnumerateEdges(points, isClosed))
        {
            var segmentLength = WallPolylineEdges.SegmentLength(start, end);
            if (segmentLength <= Epsilon)
            {
                continue;
            }

            var edgeStart = WallPolylineEdges.EdgeStartArcLength(points, arcLengths, startIndex, isClosed);
            var closest = ProjectOntoSegment(target, start, end, out var t);
            var dx = target.X - closest.X;
            var dy = target.Y - closest.Y;
            var distanceSquared = dx * dx + dy * dy;

            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                bestArcLength = edgeStart + t * segmentLength;
            }
        }

        return bestArcLength;
    }

    private static MapPoint InterpolateAtLength(IList<MapPoint> points, bool isClosed, double length)
    {
        var totalLength = WallPolylineEdges.TotalLength(points, isClosed);
        if (totalLength <= Epsilon)
        {
            return points[0];
        }

        if (length <= Epsilon)
        {
            return points[0];
        }

        if (length >= totalLength - Epsilon)
        {
            return isClosed ? points[0] : points[^1];
        }

        var traversed = 0d;
        foreach (var (start, end, _) in WallPolylineEdges.EnumerateEdges(points, isClosed))
        {
            var segmentLength = WallPolylineEdges.SegmentLength(start, end);
            if (segmentLength <= Epsilon)
            {
                continue;
            }

            if (length <= traversed + segmentLength + Epsilon)
            {
                var t = (length - traversed) / segmentLength;
                return new MapPoint(
                    start.X + t * (end.X - start.X),
                    start.Y + t * (end.Y - start.Y));
            }

            traversed += segmentLength;
        }

        return points[^1];
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
