using Brickwork.Core.Models;

namespace Brickwork.Core.Geometry;

public static class RegionGeometryEditing
{
    public static void SetVertexPosition(Region region, int vertexIndex, MapPoint scenePoint) =>
        WallGeometryEditing.SetVertexPosition(AsClosedWall(region), vertexIndex, scenePoint);

    public static int? TryInsertVertex(Region region, MapPoint scenePoint, double minDistanceFromExisting = 1d) =>
        WallGeometryEditing.TryInsertVertex(AsClosedWall(region), scenePoint, minDistanceFromExisting);

    public static bool TryRemoveVertex(Region region, int vertexIndex) =>
        WallGeometryEditing.TryRemoveVertex(AsClosedWall(region), vertexIndex);

    private static Wall AsClosedWall(Region region) =>
        new()
        {
            IsClosed = true,
            Points = region.Points,
        };
}
