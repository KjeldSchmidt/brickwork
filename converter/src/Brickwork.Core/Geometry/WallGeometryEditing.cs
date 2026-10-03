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

    /// <summary>
    /// Sets wall thickness from the perpendicular scene distance of
    /// <paramref name="scenePoint"/> to the terrain thickness handle centerline.
    /// Stored as entity-local <see cref="Wall.WallThickness"/> (scene = value × scale).
    /// </summary>
    public static void SetTerrainThicknessFromScene(Wall wall, MapPoint scenePoint)
    {
        if (!TryGetTerrainThicknessHandleArcLength(wall, out var arcLength))
        {
            return;
        }

        var center = WallPathSegmentBuilder.GetScenePointAtArcLength(wall, arcLength);
        var tangent = WallPathSegmentBuilder.GetTangentAtArcLength(wall, arcLength);
        var dx = scenePoint.X - center.X;
        var dy = scenePoint.Y - center.Y;
        // Perpendicular distance to the centerline (normal = rotate tangent 90°).
        var halfSceneThickness = Math.Abs(dx * (-tangent.Y) + dy * tangent.X);
        var scale = wall.Scale > Epsilon ? wall.Scale : 1d;
        const double minThickness = 2d;
        wall.WallThickness = Math.Max(minThickness, (halfSceneThickness * 2d) / scale);
    }

    /// <summary>
    /// Picks an arc length for the terrain thickness handle: midpoint of the longest
    /// non-portal span between points of interest (wall ends + portal ends).
    /// Falls back to the wall start when the path is fully covered by portals.
    /// </summary>
    public static bool TryGetTerrainThicknessHandleArcLength(Wall wall, out double arcLength)
    {
        arcLength = 0d;
        if (wall.LineType != WallLineType.Terrain || wall.Points.Count < 2)
        {
            return false;
        }

        var totalLength = WallPolylineEdges.TotalLength(wall.Points, wall.IsClosed);
        if (totalLength <= Epsilon)
        {
            return false;
        }

        var pointsOfInterest = CollectThicknessHandlePointsOfInterest(wall, totalLength);
        var bestGapLength = -1d;
        var bestMid = 0d;
        var foundNonPortalGap = false;

        for (var index = 0; index < pointsOfInterest.Count; index++)
        {
            double gapStart;
            double gapEnd;
            if (index + 1 < pointsOfInterest.Count)
            {
                gapStart = pointsOfInterest[index];
                gapEnd = pointsOfInterest[index + 1];
            }
            else if (wall.IsClosed)
            {
                gapStart = pointsOfInterest[index];
                gapEnd = pointsOfInterest[0] + totalLength;
            }
            else
            {
                break;
            }

            var gapLength = gapEnd - gapStart;
            if (gapLength <= Epsilon)
            {
                continue;
            }

            var mid = wall.IsClosed
                ? WallCircularIntervals.NormalizeArcLength((gapStart + gapEnd) / 2d, totalLength)
                : (gapStart + gapEnd) / 2d;

            if (IsArcLengthInsideAnyPortal(wall, mid, totalLength))
            {
                continue;
            }

            if (gapLength > bestGapLength + Epsilon)
            {
                bestGapLength = gapLength;
                bestMid = mid;
                foundNonPortalGap = true;
            }
        }

        arcLength = foundNonPortalGap ? bestMid : 0d;
        return true;
    }

    private static List<double> CollectThicknessHandlePointsOfInterest(Wall wall, double totalLength)
    {
        var points = new List<double>();
        var portalBoundaryCount = 0;

        foreach (var portal in wall.Portals)
        {
            if (!WallPathSegmentBuilder.TryGetPortalArcInterval(wall, portal, out var start, out var end))
            {
                continue;
            }

            var center = (start + end) / 2d;
            var halfWidth = (end - start) / 2d;
            foreach (var (gapStart, gapEnd) in WallCircularIntervals.ExpandPortalGap(
                         center,
                         halfWidth,
                         totalLength,
                         wall.IsClosed))
            {
                points.Add(gapStart);
                points.Add(gapEnd);
                portalBoundaryCount += 2;
            }
        }

        // Closed loops already wrap; the seam is not a real endpoint. Including it when portals
        // exist splits one continuous non-portal span into two (portal-end / seam / portal-end)
        // and makes the handle jump as portals move. Only use the seam when there are no portals.
        if (!wall.IsClosed)
        {
            points.Add(0d);
            points.Add(totalLength);
        }
        else if (portalBoundaryCount == 0)
        {
            points.Add(0d);
        }

        var normalized = new List<double>(points.Count);
        foreach (var point in points)
        {
            var value = wall.IsClosed
                ? WallCircularIntervals.NormalizeArcLength(point, totalLength)
                : Math.Clamp(point, 0d, totalLength);

            if (normalized.Any(existing => Math.Abs(existing - value) <= Epsilon))
            {
                continue;
            }

            normalized.Add(value);
        }

        normalized.Sort();
        return normalized;
    }

    private static bool IsArcLengthInsideAnyPortal(Wall wall, double arcLength, double totalLength)
    {
        foreach (var portal in wall.Portals)
        {
            if (!WallPathSegmentBuilder.TryGetPortalArcInterval(wall, portal, out var start, out var end))
            {
                continue;
            }

            var center = (start + end) / 2d;
            var halfWidth = (end - start) / 2d;
            foreach (var (gapStart, gapEnd) in WallCircularIntervals.ExpandPortalGap(
                         center,
                         halfWidth,
                         totalLength,
                         wall.IsClosed))
            {
                if (arcLength + Epsilon >= gapStart && arcLength <= gapEnd + Epsilon)
                {
                    return true;
                }
            }
        }

        return false;
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
