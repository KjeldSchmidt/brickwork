using Brickwork.Core.Geometry;
using Brickwork.Core.Models;

namespace Brickwork.App;

public static class WallMarqueePicker
{
    public static IReadOnlyList<int> PickWallIds(
        MapDocument map,
        double left,
        double top,
        double right,
        double bottom)
    {
        var transform = SceneTransform.FromMap(map);
        if (transform is null || right - left < 1e-6 || bottom - top < 1e-6)
        {
            return [];
        }

        var ids = new List<int>();
        foreach (var wall in map.Walls)
        {
            if (!wall.WallEnabled || wall.Points.Count < 2)
            {
                continue;
            }

            if (WallIntersectsRect(wall, transform, left, top, right, bottom))
            {
                ids.Add(wall.EntityId);
            }
        }

        return ids;
    }

    private static bool WallIntersectsRect(
        Wall wall,
        SceneTransform transform,
        double left,
        double top,
        double right,
        double bottom)
    {
        foreach (var segment in WallPathSegmentBuilder.BuildSegments(wall))
        {
            var points = segment.Points;
            var count = points.Count;
            var edgeCount = WallPolylineEdges.EdgeCount(count, segment.IsClosed);
            for (var i = 0; i < edgeCount; i++)
            {
                var a = transform.SceneToPreview(points[i]);
                var b = transform.SceneToPreview(points[(i + 1) % count]);
                if (SegmentIntersectsOrInsideRect(a, b, left, top, right, bottom))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool SegmentIntersectsOrInsideRect(
        MapPoint a,
        MapPoint b,
        double left,
        double top,
        double right,
        double bottom)
    {
        if (PointInRect(a, left, top, right, bottom) || PointInRect(b, left, top, right, bottom))
        {
            return true;
        }

        return SegmentsIntersect(a, b, new MapPoint(left, top), new MapPoint(right, top))
            || SegmentsIntersect(a, b, new MapPoint(right, top), new MapPoint(right, bottom))
            || SegmentsIntersect(a, b, new MapPoint(right, bottom), new MapPoint(left, bottom))
            || SegmentsIntersect(a, b, new MapPoint(left, bottom), new MapPoint(left, top));
    }

    private static bool PointInRect(MapPoint point, double left, double top, double right, double bottom) =>
        point.X >= left && point.X <= right && point.Y >= top && point.Y <= bottom;

    private static bool SegmentsIntersect(MapPoint a1, MapPoint a2, MapPoint b1, MapPoint b2)
    {
        var d1 = Cross(a2, b1, a1);
        var d2 = Cross(a2, b2, a1);
        var d3 = Cross(b2, a1, b1);
        var d4 = Cross(b2, a2, b1);

        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
            ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
        {
            return true;
        }

        return false;
    }

    private static double Cross(MapPoint a, MapPoint b, MapPoint origin) =>
        ((b.X - origin.X) * (a.Y - origin.Y)) - ((b.Y - origin.Y) * (a.X - origin.X));
}
