using InkarnateTools.Core.Models;

namespace InkarnateTools.Core.Geometry;

public static class WallThicknessPolygonBuilder
{
    private const double Epsilon = 1e-9;
    private const double MiterLimit = 4d;

    public static IReadOnlyList<MapPoint> BuildOutline(
        IEnumerable<MapPoint> centerline,
        double thickness,
        bool isClosed = false)
    {
        if (isClosed)
        {
            return [];
        }

        var points = centerline as IReadOnlyList<MapPoint> ?? centerline.ToList();
        if (points.Count < 2 || thickness <= Epsilon)
        {
            return [];
        }

        var half = thickness / 2d;
        var leftSide = new List<MapPoint>();
        var rightSide = new List<MapPoint>();
        var count = points.Count;

        var startNormal = LeftNormal(points[0], points[1]);
        leftSide.Add(Offset(points[0], startNormal, half));
        rightSide.Add(Offset(points[0], startNormal, -half));

        for (var i = 1; i < count - 1; i++)
        {
            ComputeJoin(points, i - 1, i, i + 1, half, leftSide, rightSide);
        }

        var endNormal = LeftNormal(points[^2], points[^1]);
        leftSide.Add(Offset(points[^1], endNormal, half));
        rightSide.Add(Offset(points[^1], endNormal, -half));

        var polygon = new List<MapPoint>(leftSide);
        for (var i = rightSide.Count - 1; i >= 0; i--)
        {
            polygon.Add(rightSide[i]);
        }

        return polygon;
    }

    public static WallTerrainRing? BuildClosedRing(IEnumerable<MapPoint> centerline, double thickness)
    {
        var points = centerline as IReadOnlyList<MapPoint> ?? centerline.ToList();
        if (points.Count < 3 || thickness <= Epsilon)
        {
            return null;
        }

        var half = thickness / 2d;
        var outer = new List<MapPoint>();
        var inner = new List<MapPoint>();
        var count = points.Count;

        for (var i = 0; i < count; i++)
        {
            var previous = (i - 1 + count) % count;
            var next = (i + 1) % count;
            ComputeJoin(points, previous, i, next, half, outer, inner);
        }

        if (Math.Abs(PolygonArea(outer)) < Math.Abs(PolygonArea(inner)))
        {
            (outer, inner) = (inner, outer);
        }

        return new WallTerrainRing(outer, inner);
    }

    public static IReadOnlyList<IReadOnlyList<MapPoint>> BuildTerrainExportLoops(
        IList<MapPoint> centerline,
        double thickness,
        bool isClosed)
    {
        if (centerline.Count < 2 || thickness <= Epsilon)
        {
            return [];
        }

        if (isClosed)
        {
            var ring = BuildClosedRing(centerline, thickness);
            if (ring is null || ring.Outer.Count < 3 || ring.Inner.Count < 3)
            {
                return [];
            }

            return [ring.Outer, ring.Inner];
        }

        var outline = BuildOutline(centerline, thickness);
        return outline.Count >= 3 ? [outline] : [];
    }

    private static double PolygonArea(IReadOnlyList<MapPoint> points)
    {
        double area = 0;
        for (var i = 0; i < points.Count; i++)
        {
            var j = (i + 1) % points.Count;
            area += points[i].X * points[j].Y;
            area -= points[j].X * points[i].Y;
        }

        return area / 2d;
    }

    private static void ComputeJoin(
        IReadOnlyList<MapPoint> centerline,
        int previousIndex,
        int index,
        int nextIndex,
        double half,
        List<MapPoint> leftSide,
        List<MapPoint> rightSide)
    {
        var previous = centerline[previousIndex];
        var current = centerline[index];
        var next = centerline[nextIndex];

        var incomingNormal = LeftNormal(previous, current);
        var outgoingNormal = LeftNormal(current, next);

        if (TryIntersectOffsetLines(previous, current, incomingNormal, current, next, outgoingNormal, half, out var leftJoin))
        {
            leftSide.Add(leftJoin);
        }
        else
        {
            leftSide.Add(Offset(current, incomingNormal, half));
        }

        if (TryIntersectOffsetLines(previous, current, incomingNormal, current, next, outgoingNormal, -half, out var rightJoin))
        {
            rightSide.Add(rightJoin);
        }
        else
        {
            rightSide.Add(Offset(current, incomingNormal, -half));
        }
    }

    private static bool TryIntersectOffsetLines(
        MapPoint line1Start,
        MapPoint line1End,
        MapPoint line1Normal,
        MapPoint line2Start,
        MapPoint line2End,
        MapPoint line2Normal,
        double half,
        out MapPoint intersection)
    {
        var a1 = Offset(line1Start, line1Normal, half);
        var a2 = Offset(line1End, line1Normal, half);
        var b1 = Offset(line2Start, line2Normal, half);
        var b2 = Offset(line2End, line2Normal, half);

        if (TryLineIntersection(a1, a2, b1, b2, out intersection))
        {
            var miterLength = Distance(intersection, Offset(line1End, line1Normal, half));
            if (miterLength <= half * MiterLimit)
            {
                return true;
            }
        }

        intersection = default;
        return false;
    }

    private static bool TryLineIntersection(
        MapPoint a1,
        MapPoint a2,
        MapPoint b1,
        MapPoint b2,
        out MapPoint intersection)
    {
        var dx1 = a2.X - a1.X;
        var dy1 = a2.Y - a1.Y;
        var dx2 = b2.X - b1.X;
        var dy2 = b2.Y - b1.Y;
        var denominator = dx1 * dy2 - dy1 * dx2;

        if (Math.Abs(denominator) <= Epsilon)
        {
            intersection = default;
            return false;
        }

        var t = ((b1.X - a1.X) * dy2 - (b1.Y - a1.Y) * dx2) / denominator;
        intersection = new MapPoint(a1.X + t * dx1, a1.Y + t * dy1);
        return true;
    }

    private static MapPoint LeftNormal(MapPoint start, MapPoint end)
    {
        var direction = Normalize(Subtract(end, start));
        return new MapPoint(-direction.Y, direction.X);
    }

    private static MapPoint Normalize(MapPoint vector)
    {
        var length = Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y);
        if (length <= Epsilon)
        {
            return new MapPoint(0, 0);
        }

        return new MapPoint(vector.X / length, vector.Y / length);
    }

    private static MapPoint Subtract(MapPoint end, MapPoint start) =>
        new(end.X - start.X, end.Y - start.Y);

    private static MapPoint Offset(MapPoint point, MapPoint normal, double distance) =>
        new(point.X + normal.X * distance, point.Y + normal.Y * distance);

    private static double Distance(MapPoint left, MapPoint right)
    {
        var dx = left.X - right.X;
        var dy = left.Y - right.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
