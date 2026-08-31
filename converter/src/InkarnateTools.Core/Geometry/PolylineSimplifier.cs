using InkarnateTools.Core.Models;

namespace InkarnateTools.Core.Geometry;

public static class PolylineSimplifier
{
    public static List<MapPoint> DouglasPeucker(IList<MapPoint> points, double tolerance)
    {
        if (points.Count <= 2 || tolerance <= 0)
        {
            return points.ToList();
        }

        var keep = new bool[points.Count];
        keep[0] = true;
        keep[^1] = true;
        DouglasPeuckerRecursive(points, 0, points.Count - 1, tolerance, keep);

        var result = new List<MapPoint>(points.Count);
        for (var i = 0; i < points.Count; i++)
        {
            if (keep[i])
            {
                result.Add(points[i]);
            }
        }

        return result;
    }

    private static void DouglasPeuckerRecursive(
        IList<MapPoint> points,
        int start,
        int end,
        double tolerance,
        bool[] keep)
    {
        if (end <= start + 1)
        {
            return;
        }

        var maxDistance = 0d;
        var index = start;

        for (var i = start + 1; i < end; i++)
        {
            var distance = PerpendicularDistance(points[i], points[start], points[end]);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                index = i;
            }
        }

        if (maxDistance <= tolerance)
        {
            return;
        }

        keep[index] = true;
        DouglasPeuckerRecursive(points, start, index, tolerance, keep);
        DouglasPeuckerRecursive(points, index, end, tolerance, keep);
    }

    private static double PerpendicularDistance(MapPoint point, MapPoint lineStart, MapPoint lineEnd)
    {
        var dx = lineEnd.X - lineStart.X;
        var dy = lineEnd.Y - lineStart.Y;
        var lengthSquared = dx * dx + dy * dy;

        if (lengthSquared <= double.Epsilon)
        {
            var px = point.X - lineStart.X;
            var py = point.Y - lineStart.Y;
            return Math.Sqrt(px * px + py * py);
        }

        var t = Math.Clamp(
            ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / lengthSquared,
            0d,
            1d);

        var projX = lineStart.X + t * dx;
        var projY = lineStart.Y + t * dy;
        var distX = point.X - projX;
        var distY = point.Y - projY;
        return Math.Sqrt(distX * distX + distY * distY);
    }
}
