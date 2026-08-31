using InkarnateTools.Core.Models;

namespace InkarnateTools.Core.Geometry;

public static class MapPointTransforms
{
    private const double Epsilon = 1e-9;

    public static MapPoint RotateAround(MapPoint point, MapPoint pivot, double angleDegrees)
    {
        if (Math.Abs(angleDegrees) <= Epsilon)
        {
            return point;
        }

        var radians = angleDegrees * (Math.PI / 180d);
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var dx = point.X - pivot.X;
        var dy = point.Y - pivot.Y;

        return new MapPoint(
            pivot.X + (dx * cos) - (dy * sin),
            pivot.Y + (dx * sin) + (dy * cos));
    }

    public static MapPoint LocalToScene(Wall wall, MapPoint local)
    {
        var scaled = new MapPoint(local.X * wall.Scale, local.Y * wall.Scale);
        var rotated = Math.Abs(wall.Angle) <= Epsilon
            ? scaled
            : RotateAround(scaled, new MapPoint(0, 0), wall.Angle);

        return new MapPoint(rotated.X + wall.PathOrigin.X, rotated.Y + wall.PathOrigin.Y);
    }

    public static void Translate(IList<MapPoint> points, double dx, double dy)
    {
        if (Math.Abs(dx) <= Epsilon && Math.Abs(dy) <= Epsilon)
        {
            return;
        }

        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];
            points[i] = new MapPoint(point.X + dx, point.Y + dy);
        }
    }

    public static void RotateAll(IList<MapPoint> points, MapPoint pivot, double angleDegrees)
    {
        if (Math.Abs(angleDegrees) <= Epsilon)
        {
            return;
        }

        for (var i = 0; i < points.Count; i++)
        {
            points[i] = RotateAround(points[i], pivot, angleDegrees);
        }
    }
}
