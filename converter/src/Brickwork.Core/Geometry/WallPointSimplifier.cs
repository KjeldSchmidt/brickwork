using Brickwork.Core.Models;

namespace Brickwork.Core.Geometry;

public static class WallPointSimplifier
{
    public static void Apply(Wall wall, double tolerance)
    {
        if (wall.Points.Count < 2)
        {
            return;
        }

        var simplified = PolylineSimplifier.DouglasPeucker(wall.Points, tolerance, wall.IsClosed);
        wall.Points.Clear();
        foreach (var point in simplified)
        {
            wall.Points.Add(point);
        }

        // Inkarnate (and simplify) can leave portal anchors off the polyline; snap like drag does.
        WallGeometryEditing.ResnapPortalAnchors(wall);
    }

    public static void ApplyAll(IEnumerable<Wall> walls, double tolerance)
    {
        foreach (var wall in walls)
        {
            Apply(wall, tolerance);
        }
    }
}
