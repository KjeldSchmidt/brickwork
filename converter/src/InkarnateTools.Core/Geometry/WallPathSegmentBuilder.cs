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

        var arcLengths = ComputeArcLengths(wall.Points);
        var totalLength = arcLengths[^1];
        if (totalLength <= Epsilon)
        {
            return [CopyPoints(wall.Points)];
        }

        var gaps = BuildGapIntervals(wall, arcLengths, totalLength);
        if (gaps.Count == 0)
        {
            return [CopyPoints(wall.Points)];
        }

        return ClipPolyline(wall.Points, arcLengths, gaps);
    }

    public static IReadOnlyList<WallPortalSegment> BuildPortalSegments(Wall wall)
    {
        if (wall.Points.Count < 2 || wall.Portals.Count == 0)
        {
            return [];
        }

        var arcLengths = ComputeArcLengths(wall.Points);
        var totalLength = arcLengths[^1];
        if (totalLength <= Epsilon)
        {
            return [];
        }

        var segments = new List<WallPortalSegment>();
        foreach (var portal in wall.Portals)
        {
            if (portal.Width <= Epsilon)
            {
                continue;
            }

            var anchorScene = PortalAnchorToScene(wall, portal);
            var center = FindArcLengthAtClosestPoint(wall.Points, arcLengths, anchorScene);
            var halfWidth = portal.Width / 2d;
            var start = Math.Max(0d, center - halfWidth);
            var end = Math.Min(totalLength, center + halfWidth);
            if (end - start <= Epsilon)
            {
                continue;
            }

            var points = ExtractIntervalPolyline(wall.Points, arcLengths, start, end);
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
            var center = FindArcLengthAtClosestPoint(wall.Points, arcLengths, anchorScene);
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
        double[] arcLengths,
        MapPoint target)
    {
        var bestDistanceSquared = double.MaxValue;
        var bestArcLength = 0d;

        for (var i = 0; i < points.Count - 1; i++)
        {
            var segmentLength = arcLengths[i + 1] - arcLengths[i];
            if (segmentLength <= Epsilon)
            {
                continue;
            }

            var closest = ProjectOntoSegment(target, points[i], points[i + 1], out var t);
            var dx = target.X - closest.X;
            var dy = target.Y - closest.Y;
            var distanceSquared = dx * dx + dy * dy;

            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                bestArcLength = arcLengths[i] + t * segmentLength;
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
        double[] arcLengths,
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

        for (var i = 0; i < points.Count - 1; i++)
        {
            var edgeStart = arcLengths[i];
            var edgeEnd = arcLengths[i + 1];
            var visibleIntervals = SubtractGaps([(edgeStart, edgeEnd)], gaps);

            foreach (var (intervalStart, intervalEnd) in visibleIntervals)
            {
                if (intervalEnd - intervalStart <= Epsilon)
                {
                    continue;
                }

                var segmentStart = InterpolateAtLength(points, arcLengths, intervalStart);
                var segmentEnd = InterpolateAtLength(points, arcLengths, intervalEnd);

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
        double[] arcLengths,
        double length)
    {
        if (length <= arcLengths[0] + Epsilon)
        {
            return points[0];
        }

        if (length >= arcLengths[^1] - Epsilon)
        {
            return points[^1];
        }

        for (var i = 0; i < arcLengths.Length - 1; i++)
        {
            if (length > arcLengths[i + 1] + Epsilon)
            {
                continue;
            }

            var segmentLength = arcLengths[i + 1] - arcLengths[i];
            if (segmentLength <= Epsilon)
            {
                return points[i + 1];
            }

            var t = (length - arcLengths[i]) / segmentLength;
            return new MapPoint(
                points[i].X + t * (points[i + 1].X - points[i].X),
                points[i].Y + t * (points[i + 1].Y - points[i].Y));
        }

        return points[^1];
    }

    private static double[] ComputeArcLengths(IList<MapPoint> points)
    {
        var arcLengths = new double[points.Count];
        for (var i = 1; i < points.Count; i++)
        {
            var dx = points[i].X - points[i - 1].X;
            var dy = points[i].Y - points[i - 1].Y;
            arcLengths[i] = arcLengths[i - 1] + Math.Sqrt(dx * dx + dy * dy);
        }

        return arcLengths;
    }

    private static bool PointsEqual(MapPoint left, MapPoint right) =>
        Math.Abs(left.X - right.X) <= Epsilon &&
        Math.Abs(left.Y - right.Y) <= Epsilon;

    private static List<MapPoint> ExtractIntervalPolyline(
        IList<MapPoint> points,
        double[] arcLengths,
        double intervalStart,
        double intervalEnd)
    {
        var polyline = new List<MapPoint> { InterpolateAtLength(points, arcLengths, intervalStart) };

        for (var i = 1; i < points.Count - 1; i++)
        {
            var vertexLength = arcLengths[i];
            if (vertexLength > intervalStart + Epsilon && vertexLength < intervalEnd - Epsilon)
            {
                polyline.Add(points[i]);
            }
        }

        var endPoint = InterpolateAtLength(points, arcLengths, intervalEnd);
        if (!PointsEqual(polyline[^1], endPoint))
        {
            polyline.Add(endPoint);
        }

        return polyline;
    }

    private static List<MapPoint> CopyPoints(IList<MapPoint> points) => points.ToList();
}
