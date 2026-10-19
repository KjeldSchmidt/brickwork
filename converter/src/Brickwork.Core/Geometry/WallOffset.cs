using Brickwork.Core.Models;

namespace Brickwork.Core.Geometry;

/// <summary>
/// Moves a wall parallel to its own centerline, e.g. to push walls drawn along the middle of an
/// Inkarnate cave-wall path out to the edge of the painted rock.
/// </summary>
public static class WallOffset
{
    private const double Epsilon = 1e-9;

    /// <summary>Miter joins longer than this multiple of the offset distance are beveled.</summary>
    private const double MiterLimit = 4d;

    /// <summary>
    /// Sets <paramref name="wall"/>'s offset to <paramref name="distance"/> scene units from its
    /// original path. The offset is absolute: applying 10 and then 5 leaves the wall 5 units away,
    /// and 0 restores the original path. Closed walls grow outward for positive values and shrink
    /// inward for negative ones; open walls move to the right of their drawing direction (as seen
    /// on screen) for positive values.
    /// </summary>
    /// <remarks>
    /// If the wall's points were edited since the last offset, the edited points become the new
    /// original path, so the edit is kept.
    /// </remarks>
    public static void Apply(Wall wall, double distance)
    {
        if (wall.Points.Count < 2)
        {
            return;
        }

        if (wall.OffsetBasePoints.Count == 0 || !MatchesOffsetResult(wall))
        {
            Replace(wall.OffsetBasePoints, wall.Points);
            wall.Offset = 0;
        }

        if (Math.Abs(distance - wall.Offset) <= Epsilon)
        {
            return;
        }

        if (Math.Abs(distance) <= Epsilon)
        {
            Replace(wall.Points, wall.OffsetBasePoints);
            wall.OffsetBasePoints.Clear();
            wall.Offset = 0;
            WallGeometryEditing.ResnapPortalAnchors(wall);
            return;
        }

        var offset = OffsetPolyline(wall.OffsetBasePoints.ToList(), distance, wall.IsClosed);
        if (offset.Count < 2)
        {
            return;
        }

        Replace(wall.Points, offset);
        wall.Offset = distance;
        WallGeometryEditing.ResnapPortalAnchors(wall);
    }

    /// <summary>The wall's path before any offset.</summary>
    public static IList<MapPoint> OriginalPath(Wall wall) =>
        wall.OffsetBasePoints.Count > 0 ? wall.OffsetBasePoints : wall.Points;

    private static bool MatchesOffsetResult(Wall wall)
    {
        var expected = Math.Abs(wall.Offset) <= Epsilon
            ? wall.OffsetBasePoints.ToList()
            : OffsetPolyline(wall.OffsetBasePoints.ToList(), wall.Offset, wall.IsClosed);
        return expected.Count == wall.Points.Count &&
               expected.Zip(wall.Points).All(pair => NearlyEqual(pair.First, pair.Second));
    }

    private static void Replace(IList<MapPoint> target, IEnumerable<MapPoint> source)
    {
        var points = source.ToList();
        target.Clear();
        foreach (var point in points)
        {
            target.Add(point);
        }
    }

    public static IReadOnlyList<MapPoint> OffsetPolyline(IReadOnlyList<MapPoint> points, double distance, bool isClosed)
    {
        var cleaned = RemoveConsecutiveDuplicates(points, isClosed);
        if (cleaned.Count < 2 || (isClosed && cleaned.Count < 3))
        {
            return points.ToList();
        }

        // Scene space is y-down, so a positive shoelace area means the left normal points inside.
        var leftDistance = isClosed
            ? (SignedArea(cleaned) > 0 ? -distance : distance)
            : distance;

        var count = cleaned.Count;
        var result = new List<MapPoint>(count);
        for (var i = 0; i < count; i++)
        {
            var current = cleaned[i];
            var hasPrevious = isClosed || i > 0;
            var hasNext = isClosed || i < count - 1;

            if (!hasPrevious)
            {
                result.Add(Offset(current, LeftNormal(current, cleaned[i + 1]), leftDistance));
                continue;
            }

            var previous = cleaned[(i - 1 + count) % count];
            if (!hasNext)
            {
                result.Add(Offset(current, LeftNormal(previous, current), leftDistance));
                continue;
            }

            var next = cleaned[(i + 1) % count];
            AddJoin(result, current, LeftNormal(previous, current), LeftNormal(current, next), leftDistance);
        }

        return result;
    }

    private static void AddJoin(
        List<MapPoint> result,
        MapPoint current,
        MapPoint incomingNormal,
        MapPoint outgoingNormal,
        double distance)
    {
        var sum = new MapPoint(incomingNormal.X + outgoingNormal.X, incomingNormal.Y + outgoingNormal.Y);
        var sumLength = Math.Sqrt(sum.X * sum.X + sum.Y * sum.Y);
        if (sumLength > Epsilon)
        {
            var miter = new MapPoint(sum.X / sumLength, sum.Y / sumLength);
            var cosHalfAngle = miter.X * incomingNormal.X + miter.Y * incomingNormal.Y;
            if (cosHalfAngle >= 1d / MiterLimit)
            {
                result.Add(Offset(current, miter, distance / cosHalfAngle));
                return;
            }
        }

        result.Add(Offset(current, incomingNormal, distance));
        result.Add(Offset(current, outgoingNormal, distance));
    }

    private static List<MapPoint> RemoveConsecutiveDuplicates(IReadOnlyList<MapPoint> points, bool isClosed)
    {
        var result = new List<MapPoint>(points.Count);
        foreach (var point in points)
        {
            if (result.Count == 0 || !NearlyEqual(result[^1], point))
            {
                result.Add(point);
            }
        }

        if (isClosed && result.Count > 1 && NearlyEqual(result[0], result[^1]))
        {
            result.RemoveAt(result.Count - 1);
        }

        return result;
    }

    private static double SignedArea(IReadOnlyList<MapPoint> points)
    {
        double area = 0;
        for (var i = 0; i < points.Count; i++)
        {
            var j = (i + 1) % points.Count;
            area += points[i].X * points[j].Y - points[j].X * points[i].Y;
        }

        return area / 2d;
    }

    private static MapPoint LeftNormal(MapPoint start, MapPoint end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        return length <= Epsilon ? new MapPoint(0, 0) : new MapPoint(-dy / length, dx / length);
    }

    private static MapPoint Offset(MapPoint point, MapPoint normal, double distance) =>
        new(point.X + normal.X * distance, point.Y + normal.Y * distance);

    private static bool NearlyEqual(MapPoint a, MapPoint b) =>
        Math.Abs(a.X - b.X) <= 1e-6 && Math.Abs(a.Y - b.Y) <= 1e-6;
}
