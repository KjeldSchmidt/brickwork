using Brickwork.Core.Models;

namespace Brickwork.Core.Geometry;

public sealed record LightPickTarget(LightSource Light);

/// <summary>Picks a light by proximity to its origin (and optionally its radius rings).</summary>
public static class LightHitTester
{
    public static LightPickTarget? Pick(
        MapDocument map,
        MapPoint previewPoint,
        double tolerancePreviewPixels,
        bool includeRings = true)
    {
        var transform = SceneTransform.ForMapPreview(map);
        if (transform is null)
        {
            return null;
        }

        LightPickTarget? best = null;
        var bestDistanceSquared = tolerancePreviewPixels * tolerancePreviewPixels;
        var cellSize = map.Grid.CellSize;
        var pixelsPerSquare = cellSize * transform.ScaleX;

        foreach (var light in map.Lights)
        {
            var center = transform.SceneToPreview(light.Position);
            var dx = previewPoint.X - center.X;
            var dy = previewPoint.Y - center.Y;
            var originDistanceSquared = dx * dx + dy * dy;
            if (originDistanceSquared <= bestDistanceSquared)
            {
                bestDistanceSquared = originDistanceSquared;
                best = new LightPickTarget(light);
            }

            if (!includeRings || !light.IsActive || pixelsPerSquare <= 0)
            {
                continue;
            }

            TryPickRing(center, light.BrightRadius * pixelsPerSquare, previewPoint, ref bestDistanceSquared, light, ref best);
            TryPickRing(center, light.DimRadius * pixelsPerSquare, previewPoint, ref bestDistanceSquared, light, ref best);
        }

        return best;
    }

    private static void TryPickRing(
        MapPoint center,
        double radiusPreview,
        MapPoint previewPoint,
        ref double bestDistanceSquared,
        LightSource light,
        ref LightPickTarget? best)
    {
        if (radiusPreview <= 1e-6)
        {
            return;
        }

        var dx = previewPoint.X - center.X;
        var dy = previewPoint.Y - center.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        var ringDistance = Math.Abs(distance - radiusPreview);
        var ringDistanceSquared = ringDistance * ringDistance;
        if (ringDistanceSquared <= bestDistanceSquared)
        {
            bestDistanceSquared = ringDistanceSquared;
            best = new LightPickTarget(light);
        }
    }
}
