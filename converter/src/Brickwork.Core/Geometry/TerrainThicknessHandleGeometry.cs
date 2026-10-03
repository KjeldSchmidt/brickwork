using Brickwork.Core.Models;

namespace Brickwork.Core.Geometry;

public enum TerrainThicknessEndpoint
{
    Negative,
    Positive,
}

public static class TerrainThicknessHandleGeometry
{
    public static bool TryGetEndpoints(
        Wall wall,
        out MapPoint center,
        out MapPoint negative,
        out MapPoint positive,
        out MapPoint tangent)
    {
        center = default;
        negative = default;
        positive = default;
        tangent = default;

        if (!WallGeometryEditing.TryGetTerrainThicknessHandleArcLength(wall, out var arcLength))
        {
            return false;
        }

        center = WallPathSegmentBuilder.GetScenePointAtArcLength(wall, arcLength);
        tangent = WallPathSegmentBuilder.GetTangentAtArcLength(wall, arcLength);
        var normal = new MapPoint(-tangent.Y, tangent.X);
        var half = wall.SceneThickness / 2d;
        negative = new MapPoint(center.X - half * normal.X, center.Y - half * normal.Y);
        positive = new MapPoint(center.X + half * normal.X, center.Y + half * normal.Y);
        return true;
    }

    /// <summary>
    /// Preview pose for an end tick whose long axis runs along the wall (across the thickness bar).
    /// </summary>
    public static (MapPoint Center, double AngleRadians) GetEndpointTickPose(
        MapPoint endpointScene,
        MapPoint wallTangentScene,
        SceneTransform transform)
    {
        var center = transform.SceneToPreview(endpointScene);
        var tangentEnd = transform.SceneToPreview(new MapPoint(
            endpointScene.X + wallTangentScene.X,
            endpointScene.Y + wallTangentScene.Y));
        var angleRadians = Math.Atan2(tangentEnd.Y - center.Y, tangentEnd.X - center.X);
        return (center, angleRadians);
    }
}
