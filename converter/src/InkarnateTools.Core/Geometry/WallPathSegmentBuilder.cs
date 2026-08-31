using InkarnateTools.Core.Models;

namespace InkarnateTools.Core.Geometry;

public sealed record WallPortalSegment(WallPortal Portal, IReadOnlyList<MapPoint> Points);

public static class WallPathSegmentBuilder
{
    private const double Epsilon = 1e-6;

    public static IReadOnlyList<IReadOnlyList<MapPoint>> BuildSegments(Wall wall)
    {
        if (wall.Points.Count < 2)
        {
            return [];
        }

        if (wall.Portals.Count == 0)
        {
            return [CopyPoints(wall.Points)];
        }

        var totalLength = WallPolylineEdges.TotalLength(wall.Points, wall.IsClosed);
        if (totalLength <= Epsilon)
        {
            return [CopyPoints(wall.Points)];
        }

        var arcLengths = WallPolylineEdges.ComputeArcLengths(wall.Points, wall.IsClosed);
        var gaps = BuildGapIntervals(wall, arcLengths, totalLength);
        if (gaps.Count == 0)
        {
            return [CopyPoints(wall.Points)];
        }

        return ClipPolyline(wall.Points, wall.IsClosed, gaps);
    }

    public static IReadOnlyList<WallExportRun> BuildExportRuns(Wall wall)
    {
        if (wall.Points.Count < 2)
        {
            return [];
        }

        if (wall.Portals.Count == 0)
        {
            return [new WallExportRun(CopyPoints(wall.Points), wall.LineType)];
        }

        var totalLength = WallPolylineEdges.TotalLength(wall.Points, wall.IsClosed);
        if (totalLength <= Epsilon)
        {
            return [new WallExportRun(CopyPoints(wall.Points), wall.LineType)];
        }

        var arcLengths = WallPolylineEdges.ComputeArcLengths(wall.Points, wall.IsClosed);
        var portalIntervals = BuildPortalIntervals(wall, arcLengths, totalLength);
        if (portalIntervals.Count == 0)
        {
            return [new WallExportRun(CopyPoints(wall.Points), wall.LineType)];
        }

        var runs = new List<WallExportRun>();
        var cursor = 0d;

        foreach (var (gapStart, gapEnd, lineType) in portalIntervals)
        {
            if (gapStart > cursor + Epsilon)
            {
                AddRunIfValid(
                    runs,
                    ExtractIntervalPolyline(wall.Points, wall.IsClosed, cursor, gapStart),
                    wall.LineType);
            }

            AddRunIfValid(
                runs,
                ExtractIntervalPolyline(wall.Points, wall.IsClosed, gapStart, gapEnd),
                lineType);

            cursor = gapEnd;
        }

        if (cursor < totalLength - Epsilon)
        {
            AddRunIfValid(
                runs,
                ExtractIntervalPolyline(wall.Points, wall.IsClosed, cursor, totalLength),
                wall.LineType);
        }

        return runs;
    }

    public static IReadOnlyList<WallPortalSegment> BuildPortalSegments(Wall wall)
    {
        if (wall.Points.Count < 2 || wall.Portals.Count == 0)
        {
            return [];
        }

        var totalLength = WallPolylineEdges.TotalLength(wall.Points, wall.IsClosed);
        if (totalLength <= Epsilon)
        {
            return [];
        }

        var arcLengths = WallPolylineEdges.ComputeArcLengths(wall.Points, wall.IsClosed);
        var segments = new List<WallPortalSegment>();
        foreach (var portal in wall.Portals)
        {
            if (portal.Width <= Epsilon)
            {
                continue;
            }

            var anchorScene = PortalAnchorToScene(wall, portal);
            var center = FindArcLengthAtClosestPoint(wall.Points, wall.IsClosed, arcLengths, anchorScene);
            var halfWidth = portal.Width / 2d;
            var start = Math.Max(0d, center - halfWidth);
            var end = Math.Min(totalLength, center + halfWidth);
            if (end - start <= Epsilon)
            {
                continue;
            }

            var points = ExtractIntervalPolyline(wall.Points, wall.IsClosed, start, end);
            if (points.Count >= 2)
            {
                segments.Add(new WallPortalSegment(portal, points));
            }
        }

        return segments;
    }

    public static MapPoint PortalAnchorToScene(Wall wall, WallPortal portal) =>
        new(
            portal.Anchor.X * wall.Scale + wall.Origin.X,
            portal.Anchor.Y * wall.Scale + wall.Origin.Y);

    private static void AddRunIfValid(
        List<WallExportRun> runs,
        IReadOnlyList<MapPoint> points,
        WallLineType lineType)
    {
        if (points.Count >= 2)
        {
            runs.Add(new WallExportRun(points, lineType));
        }
    }

    private sealed record PortalInterval(double Start, double End, WallLineType LineType);

    private static List<PortalInterval> BuildPortalIntervals(
        Wall wall,
        double[] arcLengths,
        double totalLength)
    {
        var intervals = new List<PortalInterval>();

        foreach (var portal in wall.Portals)
        {
            if (portal.Width <= Epsilon)
            {
                continue;
            }

            var anchorScene = PortalAnchorToScene(wall, portal);
            var center = FindArcLengthAtClosestPoint(wall.Points, wall.IsClosed, arcLengths, anchorScene);
            var halfWidth = portal.Width / 2d;
            var start = Math.Max(0d, center - halfWidth);
            var end = Math.Min(totalLength, center + halfWidth);

            if (end - start > Epsilon)
            {
                intervals.Add(new PortalInterval(start, end, portal.LineType));
            }
        }

        return MergePortalIntervals(intervals);
    }

    private static List<PortalInterval> MergePortalIntervals(List<PortalInterval> intervals)
    {
        if (intervals.Count <= 1)
        {
            return intervals;
        }

        intervals.Sort((left, right) => left.Start.CompareTo(right.Start));

        var merged = new List<PortalInterval> { intervals[0] };
        for (var i = 1; i < intervals.Count; i++)
        {
            var current = intervals[i];
            var last = merged[^1];

            if (current.Start <= last.End + Epsilon)
            {
                merged[^1] = new PortalInterval(
                    last.Start,
                    Math.Max(last.End, current.End),
                    last.LineType);
            }
            else
            {
                merged.Add(current);
            }
        }

        return merged;
    }

    private static List<(double Start, double End)> BuildGapIntervals(
        Wall wall,
        double[] arcLengths,
        double totalLength)
    {
        var gaps = new List<(double Start, double End)>();

        foreach (var portal in wall.Portals)
        {
            if (portal.Width <= Epsilon)
            {
                continue;
            }

            var anchorScene = PortalAnchorToScene(wall, portal);
            var center = FindArcLengthAtClosestPoint(wall.Points, wall.IsClosed, arcLengths, anchorScene);
            var halfWidth = portal.Width / 2d;
            var start = Math.Max(0d, center - halfWidth);
            var end = Math.Min(totalLength, center + halfWidth);

            if (end - start > Epsilon)
            {
                gaps.Add((start, end));
            }
        }

        return MergeIntervals(gaps);
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

    private static List<(double Start, double End)> MergeIntervals(
        List<(double Start, double End)> intervals)
    {
        if (intervals.Count <= 1)
        {
            return intervals;
        }

        intervals.Sort((left, right) => left.Start.CompareTo(right.Start));

        var merged = new List<(double Start, double End)> { intervals[0] };
        for (var i = 1; i < intervals.Count; i++)
        {
            var current = intervals[i];
            var last = merged[^1];

            if (current.Start <= last.End + Epsilon)
            {
                merged[^1] = (last.Start, Math.Max(last.End, current.End));
            }
            else
            {
                merged.Add(current);
            }
        }

        return merged;
    }

    private static List<IReadOnlyList<MapPoint>> ClipPolyline(
        IList<MapPoint> points,
        bool isClosed,
        IReadOnlyList<(double Start, double End)> gaps)
    {
        var segments = new List<IReadOnlyList<MapPoint>>();
        List<MapPoint>? currentSegment = null;

        void FlushSegment()
        {
            if (currentSegment is { Count: >= 2 })
            {
                segments.Add(currentSegment);
            }

            currentSegment = null;
        }

        var arcLengths = WallPolylineEdges.ComputeArcLengths(points, isClosed);

        foreach (var (_, _, startIndex) in WallPolylineEdges.EnumerateEdges(points, isClosed))
        {
            var edgeStart = WallPolylineEdges.EdgeStartArcLength(points, arcLengths, startIndex, isClosed);
            var edgeEnd = WallPolylineEdges.EdgeEndArcLength(points, arcLengths, startIndex, isClosed);
            var visibleIntervals = SubtractGaps([(edgeStart, edgeEnd)], gaps);

            foreach (var (intervalStart, intervalEnd) in visibleIntervals)
            {
                if (intervalEnd - intervalStart <= Epsilon)
                {
                    continue;
                }

                var segmentStart = InterpolateAtLength(points, isClosed, intervalStart);
                var segmentEnd = InterpolateAtLength(points, isClosed, intervalEnd);

                if (currentSegment is null)
                {
                    currentSegment = [segmentStart, segmentEnd];
                    continue;
                }

                var lastPoint = currentSegment[^1];
                if (PointsEqual(lastPoint, segmentStart))
                {
                    if (!PointsEqual(lastPoint, segmentEnd))
                    {
                        currentSegment.Add(segmentEnd);
                    }
                }
                else
                {
                    FlushSegment();
                    currentSegment = [segmentStart, segmentEnd];
                }
            }
        }

        FlushSegment();
        return segments;
    }

    private static List<(double Start, double End)> SubtractGaps(
        List<(double Start, double End)> intervals,
        IReadOnlyList<(double Start, double End)> gaps)
    {
        var result = intervals;

        foreach (var gap in gaps)
        {
            var next = new List<(double Start, double End)>();

            foreach (var interval in result)
            {
                if (gap.End <= interval.Start + Epsilon || gap.Start >= interval.End - Epsilon)
                {
                    next.Add(interval);
                    continue;
                }

                if (interval.Start < gap.Start - Epsilon)
                {
                    next.Add((interval.Start, gap.Start));
                }

                if (interval.End > gap.End + Epsilon)
                {
                    next.Add((gap.End, interval.End));
                }
            }

            result = next;
        }

        return result;
    }

    private static MapPoint InterpolateAtLength(
        IList<MapPoint> points,
        bool isClosed,
        double length)
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

    private static bool PointsEqual(MapPoint left, MapPoint right) =>
        Math.Abs(left.X - right.X) <= Epsilon &&
        Math.Abs(left.Y - right.Y) <= Epsilon;

    private static List<MapPoint> ExtractIntervalPolyline(
        IList<MapPoint> points,
        bool isClosed,
        double intervalStart,
        double intervalEnd)
    {
        var polyline = new List<MapPoint> { InterpolateAtLength(points, isClosed, intervalStart) };
        var totalLength = WallPolylineEdges.TotalLength(points, isClosed);

        foreach (var point in points)
        {
            var vertexLength = FindArcLengthAtClosestPoint(
                points,
                isClosed,
                WallPolylineEdges.ComputeArcLengths(points, isClosed),
                point);

            if (vertexLength > intervalStart + Epsilon && vertexLength < intervalEnd - Epsilon)
            {
                if (!PointsEqual(polyline[^1], point))
                {
                    polyline.Add(point);
                }
            }
        }

        if (isClosed && intervalEnd >= totalLength - Epsilon && !PointsEqual(polyline[^1], points[0]))
        {
            polyline.Add(points[0]);
        }

        var endPoint = InterpolateAtLength(points, isClosed, intervalEnd);
        if (!PointsEqual(polyline[^1], endPoint))
        {
            polyline.Add(endPoint);
        }

        return polyline;
    }

    private static List<MapPoint> CopyPoints(IList<MapPoint> points) => points.ToList();
}
