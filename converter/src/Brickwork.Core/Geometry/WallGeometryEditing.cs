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
        ResnapPortalAnchors(wall);
    }

    /// <summary>
    /// Keeps portal anchors on the wall centerline after polyline edits so handles stay with segments.
    /// </summary>
    public static void ResnapPortalAnchors(Wall wall)
    {
        if (wall.Portals.Count == 0 || wall.Points.Count < 2)
        {
            return;
        }

        foreach (var portal in wall.Portals)
        {
            var scene = WallPathSegmentBuilder.PortalAnchorToScene(wall, portal);
            var snapped = SnapToCenterline(wall, scene);
            portal.Anchor = MapPointTransforms.SceneToLocal(wall, snapped);
        }
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
            totalLength);

        var halfWidth = wall.IsClosed
            ? ShortestArcDistance(center, draggedArc, totalLength)
            : Math.Abs(draggedArc - center);

        const double minWidth = 2d;
        halfWidth = Math.Max(halfWidth, minWidth / 2d);
        halfWidth = Math.Min(
            halfWidth,
            WallCircularIntervals.MaxPortalHalfWidth(center, totalLength, wall.IsClosed));

        portal.Width = halfWidth * 2d;
    }

    private static double ShortestArcDistance(double from, double to, double totalLength)
    {
        var forward = WallCircularIntervals.ForwardArcDistance(from, to, totalLength);
        var backward = WallCircularIntervals.ForwardArcDistance(to, from, totalLength);
        return Math.Min(forward, backward);
    }

    private static double FindDraggedArcLength(
        Wall wall,
        double[] arcLengths,
        MapPoint snappedScene,
        double center,
        double endpointHint,
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
            var halfWidth = ShortestArcDistance(center, candidate, totalLength);

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

    /// <summary>
    /// Inserts a vertex on the closest wall edge at the projection of <paramref name="scenePoint"/>.
    /// Returns the new vertex index, or null if the click is too close to an existing vertex.
    /// </summary>
    public static int? TryInsertVertex(Wall wall, MapPoint scenePoint, double minDistanceFromExisting = 1d)
    {
        if (wall.Points.Count < 2)
        {
            return null;
        }

        var bestDistanceSquared = double.MaxValue;
        var bestStartIndex = -1;
        var bestPoint = scenePoint;
        var bestT = 0d;

        foreach (var (start, end, startIndex) in WallPolylineEdges.EnumerateEdges(wall.Points, wall.IsClosed))
        {
            var closest = ProjectOntoSegment(scenePoint, start, end, out var t);
            var dx = scenePoint.X - closest.X;
            var dy = scenePoint.Y - closest.Y;
            var distanceSquared = dx * dx + dy * dy;
            if (distanceSquared + Epsilon >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            bestStartIndex = startIndex;
            bestPoint = closest;
            bestT = t;
        }

        if (bestStartIndex < 0)
        {
            return null;
        }

        // Too close to an endpoint — that vertex already exists.
        if (bestT <= Epsilon || bestT >= 1d - Epsilon)
        {
            return null;
        }

        var minDistanceSquared = minDistanceFromExisting * minDistanceFromExisting;
        foreach (var existing in wall.Points)
        {
            var dx = existing.X - bestPoint.X;
            var dy = existing.Y - bestPoint.Y;
            if (dx * dx + dy * dy <= minDistanceSquared)
            {
                return null;
            }
        }

        var insertIndex = bestStartIndex + 1;
        wall.Points.Insert(insertIndex, bestPoint);
        ResnapPortalAnchors(wall);
        return insertIndex;
    }

    /// <summary>
    /// Removes a wall polyline vertex. Returns false if the index is invalid or the wall
    /// would drop below the minimum vertex count (2 open / 3 closed).
    /// </summary>
    public static bool TryRemoveVertex(Wall wall, int vertexIndex)
    {
        if (vertexIndex < 0 || vertexIndex >= wall.Points.Count)
        {
            return false;
        }

        var minimumCount = wall.IsClosed ? 3 : 2;
        if (wall.Points.Count <= minimumCount)
        {
            return false;
        }

        wall.Points.RemoveAt(vertexIndex);
        ResnapPortalAnchors(wall);
        return true;
    }

    public static bool TryRemovePortal(Wall wall, WallPortal portal)
    {
        if (!wall.Portals.Remove(portal))
        {
            var match = wall.Portals.FirstOrDefault(candidate => ReferenceEquals(candidate, portal))
                ?? wall.Portals.FirstOrDefault(candidate =>
                    !string.IsNullOrEmpty(portal.Id) && candidate.Id == portal.Id);
            if (match is null || !wall.Portals.Remove(match))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Adds a door portal snapped to the wall centerline at <paramref name="scenePoint"/>.
    /// </summary>
    public static WallPortal? TryAddPortal(Wall wall, MapPoint scenePoint, double defaultWidth)
    {
        if (wall.Points.Count < 2 || defaultWidth <= Epsilon)
        {
            return null;
        }

        var snapped = SnapToCenterline(wall, scenePoint);
        var portal = new WallPortal
        {
            Id = Guid.NewGuid().ToString("N"),
            Anchor = MapPointTransforms.SceneToLocal(wall, snapped),
            Width = defaultWidth,
            IsActive = true,
            LineType = WallLineType.Door,
        };
        wall.Portals.Add(portal);
        return portal;
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
