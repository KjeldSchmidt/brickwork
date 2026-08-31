using InkarnateTools.Core.Models;

namespace InkarnateTools.Core.Geometry;

public static class WallPointSimplifier
{
    public static void Apply(Wall wall, double tolerance)
    {
        if (wall.RawPoints.Count < 2)
        {
            return;
        }

        var simplified = PolylineSimplifier.DouglasPeucker(wall.RawPoints, tolerance);
        wall.Points.Clear();
        foreach (var point in simplified)
        {
            wall.Points.Add(point);
        }
    }

    public static void ApplyAll(IEnumerable<Wall> walls, double tolerance)
    {
        foreach (var wall in walls)
        {
            Apply(wall, tolerance);
        }
    }
}
