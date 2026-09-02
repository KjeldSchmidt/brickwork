using Brickwork.Core.Models;

namespace Brickwork.Core.Geometry;

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
        var anchorScene = WallPathSegmentBuilder.PortalAnchorToScene(wall, portal);
        var center = FindArcLengthAtClosestPoint(wall.Points, wall.IsClosed, arcLengths, anchorScene);
        var endpointHint = endpoint == PortalWidthEndpoint.Start ? currentStart : currentEnd;
        var draggedArc = FindDraggedArcLength(
            wall,
            arcLengths,
            snappedScene,
            center,
            endpointHint,
            endpoint,
            totalLength);

        var halfWidth = endpoint switch
        {
            PortalWidthEndpoint.End when wall.IsClosed =>
                WallCircularIntervals.ForwardArcDistance(center, draggedArc, totalLength),
            PortalWidthEndpoint.Start when wall.IsClosed =>
                WallCircularIntervals.ForwardArcDistance(draggedArc, center, totalLength),
            PortalWidthEndpoint.End => Math.Max(0d, draggedArc - center),
            PortalWidthEndpoint.Start => Math.Max(0d, center - draggedArc),
            _ => 0d,
        };

        const double minWidth = 2d;
        halfWidth = Math.Max(halfWidth, minWidth / 2d);
        halfWidth = Math.Min(
            halfWidth,
            WallCircularIntervals.MaxPortalHalfWidth(center, totalLength, wall.IsClosed));

        portal.Width = halfWidth * 2d;
    }

    private static double FindDraggedArcLength(
        Wall wall,
        double[] arcLengths,
        MapPoint snappedScene,
        double center,
        double endpointHint,
        PortalWidthEndpoint endpoint,
        double totalLength)
    {
        var baseArc = FindArcLengthAtClosestPoint(wall.Points, wall.IsClosed, arcLengths, snappedScene);
        if (!wall.IsClosed)
        {
            return baseArc;
        }

        var maxHalfWidth = totalLength / 2d;
        var bestArc = endpointHint;
        var bestScore = double.MaxValue;

        for (var branch = -1; branch <= 1; branch++)
        {
            var candidate = baseArc + branch * totalLength;
            var halfWidth = endpoint switch
            {
                PortalWidthEndpoint.End =>
                    WallCircularIntervals.ForwardArcDistance(center, candidate, totalLength),
                PortalWidthEndpoint.Start =>
                    WallCircularIntervals.ForwardArcDistance(candidate, center, totalLength),
                _ => 0d,
            };

            if (halfWidth > maxHalfWidth + Epsilon)
            {
                continue;
            }

            var score = Math.Abs(candidate - endpointHint);
            if (score + Epsilon < bestScore)
            {
                bestScore = score;
                bestArc = candidate;
            }
        }

        return bestArc;
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
