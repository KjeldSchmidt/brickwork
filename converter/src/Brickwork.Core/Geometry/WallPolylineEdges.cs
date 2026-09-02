using Brickwork.Core.Models;

namespace Brickwork.Core.Geometry;

public static class WallPolylineEdges
{
    public static int EdgeCount(int pointCount, bool isClosed)
    {
        if (pointCount < 2)
        {
            return 0;
        }

        return isClosed ? pointCount : pointCount - 1;
    }

    public static int EdgeCount(IList<MapPoint> points, bool isClosed) =>
        EdgeCount(points.Count, isClosed);

    public static IEnumerable<(MapPoint Start, MapPoint End, int StartIndex)> EnumerateEdges(
        IList<MapPoint> points,
        bool isClosed)
    {
        var edgeCount = EdgeCount(points, isClosed);
        for (var i = 0; i < edgeCount; i++)
        {
            var startIndex = i;
            var endIndex = (i + 1) % points.Count;
            yield return (points[startIndex], points[endIndex], startIndex);
        }
    }

    public static double SegmentLength(MapPoint start, MapPoint end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public static double TotalLength(IList<MapPoint> points, bool isClosed)
    {
        if (points.Count < 2)
        {
            return 0;
        }

        var total = 0d;
        foreach (var (start, end, _) in EnumerateEdges(points, isClosed))
        {
            total += SegmentLength(start, end);
        }

        return total;
    }

    public static double[] ComputeArcLengths(IList<MapPoint> points, bool isClosed)
    {
        var count = points.Count;
        var arcLengths = new double[count];
        if (count < 2)
        {
            return arcLengths;
        }

        var cumulative = 0d;
        for (var i = 1; i < count; i++)
        {
            cumulative += SegmentLength(points[i - 1], points[i]);
            arcLengths[i] = cumulative;
        }

        return arcLengths;
    }

    public static double EdgeStartArcLength(
        IList<MapPoint> points,
        double[] arcLengths,
        int edgeStartIndex,
        bool isClosed)
    {
        return edgeStartIndex == 0 ? 0d : arcLengths[edgeStartIndex];
    }

    public static double EdgeEndArcLength(
        IList<MapPoint> points,
        double[] arcLengths,
        int edgeStartIndex,
        bool isClosed)
    {
        var nextIndex = (edgeStartIndex + 1) % points.Count;
        if (nextIndex == 0 && isClosed)
        {
            return TotalLength(points, isClosed);
        }

        return arcLengths[nextIndex];
    }
}
