using Brickwork.Core.Models;

namespace Brickwork.Exporters.Uvtt;

internal static class UvttCoordinateTransform
{
    private const double Epsilon = 1e-6;

    public static MapPoint GridToScene(MapPoint gridPoint, MapPoint origin, double cellSize) =>
        new((gridPoint.X - origin.X) * cellSize, (gridPoint.Y - origin.Y) * cellSize);

    public static MapPoint SceneToGrid(MapPoint scenePoint, MapPoint origin, double cellSize)
    {
        if (cellSize <= Epsilon)
        {
            cellSize = 1d;
        }

        return new MapPoint(scenePoint.X / cellSize + origin.X, scenePoint.Y / cellSize + origin.Y);
    }

    public static UvttPoint ToUvttPoint(MapPoint gridPoint) =>
        new() { X = gridPoint.X, Y = gridPoint.Y };

    public static MapPoint FromUvttPoint(UvttPoint point) =>
        new(point.X, point.Y);

    public static bool IsClosedPolyline(IReadOnlyList<MapPoint> points)
    {
        if (points.Count < 3)
        {
            return false;
        }

        var first = points[0];
        var last = points[^1];
        return Math.Abs(first.X - last.X) <= Epsilon &&
               Math.Abs(first.Y - last.Y) <= Epsilon;
    }
}
