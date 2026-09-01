using InkarnateTools.Core.Models;

namespace InkarnateTools.Core.Geometry;

public enum PortalWidthEndpoint
{
    Start,
    End,
}

public sealed record WallVertexPickTarget(
    Wall Wall,
    int? VertexIndex,
    WallPortal? Portal,
    PortalWidthEndpoint? PortalWidthEndpoint = null);

public static class WallVertexHitTester
{
    private const double TickHalfLength = 4d;
    private const double TickHalfThickness = 2d;
    private const double TickPickPadding = 2d;

    public static WallVertexPickTarget? Pick(
        MapDocument map,
        MapPoint previewPoint,
        double tolerancePreviewPixels)
    {
        var transform = SceneTransform.FromMap(map);
        if (transform is null)
        {
            return null;
        }

        WallVertexPickTarget? bestTarget = null;
        var bestDistanceSquared = tolerancePreviewPixels * tolerancePreviewPixels;

        foreach (var wall in map.Walls)
        {
            if (!wall.WallEnabled || wall.Points.Count < 2)
            {
                continue;
            }

            for (var index = 0; index < wall.Points.Count; index++)
            {
                if (TryPickPoint(
                        transform.SceneToPreview(wall.Points[index]),
                        previewPoint,
                        ref bestDistanceSquared))
                {
                    bestTarget = new WallVertexPickTarget(wall, index, null);
                }
            }

            foreach (var portal in wall.Portals)
            {
                if (WallPathSegmentBuilder.TryGetPortalArcInterval(wall, portal, out var start, out var end))
                {
                    TryPickPortalWidthTick(
                        wall,
                        portal,
                        PortalWidthEndpoint.Start,
                        start,
                        transform,
                        previewPoint,
                        ref bestTarget,
                        ref bestDistanceSquared);

                    TryPickPortalWidthTick(
                        wall,
                        portal,
                        PortalWidthEndpoint.End,
                        end,
                        transform,
                        previewPoint,
                        ref bestTarget,
                        ref bestDistanceSquared);
                }

                var anchorScene = WallPathSegmentBuilder.PortalAnchorToScene(wall, portal);
                if (TryPickPoint(transform.SceneToPreview(anchorScene), previewPoint, ref bestDistanceSquared))
                {
                    bestTarget = new WallVertexPickTarget(wall, null, portal);
                }
            }
        }

        return bestTarget;
    }

    private static void TryPickPortalWidthTick(
        Wall wall,
        WallPortal portal,
        PortalWidthEndpoint endpoint,
        double arcLength,
        SceneTransform transform,
        MapPoint previewPoint,
        ref WallVertexPickTarget? bestTarget,
        ref double bestDistanceSquared)
    {
        var (center, angleRadians) = PortalWidthHandleGeometry.GetPreviewTickPose(wall, arcLength, transform);
        if (!TryPickOrientedRect(
                previewPoint,
                center,
                angleRadians,
                TickHalfLength + TickPickPadding,
                TickHalfThickness + TickPickPadding,
                out var distanceSquared) ||
            distanceSquared > bestDistanceSquared)
        {
            return;
        }

        bestDistanceSquared = distanceSquared;
        bestTarget = new WallVertexPickTarget(wall, null, portal, endpoint);
    }

    private static bool TryPickPoint(
        MapPoint targetPreviewPoint,
        MapPoint previewPoint,
        ref double bestDistanceSquared)
    {
        var dx = previewPoint.X - targetPreviewPoint.X;
        var dy = previewPoint.Y - targetPreviewPoint.Y;
        var distanceSquared = dx * dx + dy * dy;
        if (distanceSquared > bestDistanceSquared)
        {
            return false;
        }

        bestDistanceSquared = distanceSquared;
        return true;
    }

    private static bool TryPickOrientedRect(
        MapPoint previewPoint,
        MapPoint center,
        double angleRadians,
        double halfLength,
        double halfThickness,
        out double distanceSquared)
    {
        var cos = Math.Cos(-angleRadians);
        var sin = Math.Sin(-angleRadians);
        var dx = previewPoint.X - center.X;
        var dy = previewPoint.Y - center.Y;
        var localX = dx * cos - dy * sin;
        var localY = dx * sin + dy * cos;

        if (Math.Abs(localX) > halfLength || Math.Abs(localY) > halfThickness)
        {
            distanceSquared = 0;
            return false;
        }

        distanceSquared = dx * dx + dy * dy;
        return true;
    }
}
