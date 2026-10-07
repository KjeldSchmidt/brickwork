using Brickwork.Core.Models;

namespace Brickwork.Core.Geometry;

public sealed record RegionVertexPickTarget(Region Region, int VertexIndex);

public static class RegionVertexHitTester
{
    public static RegionVertexPickTarget? Pick(
        MapDocument map,
        MapPoint previewPoint,
        double tolerancePreviewPixels)
    {
        var transform = SceneTransform.FromMap(map);
        if (transform is null)
        {
            return null;
        }

        RegionVertexPickTarget? bestTarget = null;
        var bestDistanceSquared = tolerancePreviewPixels * tolerancePreviewPixels;

        foreach (var region in map.Regions)
        {
            if (!region.IsActive || region.Points.Count < 3)
            {
                continue;
            }

            for (var index = 0; index < region.Points.Count; index++)
            {
                var preview = transform.SceneToPreview(region.Points[index]);
                var dx = previewPoint.X - preview.X;
                var dy = previewPoint.Y - preview.Y;
                var distanceSquared = (dx * dx) + (dy * dy);
                if (distanceSquared > bestDistanceSquared)
                {
                    continue;
                }

                bestDistanceSquared = distanceSquared;
                bestTarget = new RegionVertexPickTarget(region, index);
            }
        }

        return bestTarget;
    }
}
