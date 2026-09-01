using InkarnateTools.Core.Models;

namespace InkarnateTools.Core.Geometry;

public static class PortalWidthHandleGeometry
{
    public static (MapPoint Center, double AngleRadians) GetPreviewTickPose(
        Wall wall,
        double arcLength,
        SceneTransform transform)
    {
        var pointScene = WallPathSegmentBuilder.GetScenePointAtArcLength(wall, arcLength);
        var tangentScene = WallPathSegmentBuilder.GetTangentAtArcLength(wall, arcLength);
        var center = transform.SceneToPreview(pointScene);
        var tangentEnd = transform.SceneToPreview(new MapPoint(
            pointScene.X + tangentScene.X,
            pointScene.Y + tangentScene.Y));
        var angleRadians = Math.Atan2(
            tangentEnd.Y - center.Y,
            tangentEnd.X - center.X) + Math.PI / 2d;

        return (center, angleRadians);
    }
}
