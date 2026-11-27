using Brickwork.Core.Models;

namespace Brickwork.Core.Geometry;

public enum LightHandleKind
{
    Origin,
    DimRing,
    BrightRing,
    BrightHandle,
    DimHandle,
}

public sealed record LightHandlePickTarget(LightSource Light, LightHandleKind Kind);

/// <summary>
/// Picks interactive light handles when the light editing tool is active.
/// Priority when radii are close: origin → side handles → bright ring → dim ring.
/// </summary>
public static class LightHandleHitTester
{
    private const double TickHalfLength = 4d;
    private const double TickHalfThickness = 2d;
    private const double TickPickPadding = 2d;
    private const double NearEqualSquares = 0.25d;

    public static LightHandlePickTarget? Pick(
        MapDocument map,
        MapPoint previewPoint,
        double tolerancePreviewPixels,
        IReadOnlySet<int>? interactiveLightIds = null)
    {
        var transform = SceneTransform.ForMapPreview(map);
        if (transform is null)
        {
            return null;
        }

        LightHandlePickTarget? best = null;
        var toleranceSquared = tolerancePreviewPixels * tolerancePreviewPixels;
        var bestScore = (Priority: int.MaxValue, DistanceSquared: toleranceSquared);
        var cellSize = map.Grid.CellSize;
        var pixelsPerSquare = cellSize * transform.ScaleX;

        foreach (var light in map.Lights)
        {
            if (interactiveLightIds is not null && !interactiveLightIds.Contains(light.EntityId))
            {
                continue;
            }

            var center = transform.SceneToPreview(light.Position);
            TryPickPoint(center, previewPoint, LightHandleKind.Origin, light, toleranceSquared, ref bestScore, ref best);

            if (cellSize > 1e-9 &&
                LightRadiusHandleGeometry.TryGetHandle(light, cellSize, LightRadiusHandleKind.Bright, out var brightHandle))
            {
                TryPickTick(
                    LightRadiusHandleGeometry.GetHandleTickPose(brightHandle, transform),
                    previewPoint,
                    LightHandleKind.BrightHandle,
                    light,
                    ref bestScore,
                    ref best);
            }

            if (cellSize > 1e-9 &&
                LightRadiusHandleGeometry.TryGetHandle(light, cellSize, LightRadiusHandleKind.Dim, out var dimHandle))
            {
                TryPickTick(
                    LightRadiusHandleGeometry.GetHandleTickPose(dimHandle, transform),
                    previewPoint,
                    LightHandleKind.DimHandle,
                    light,
                    ref bestScore,
                    ref best);
            }

            if (!light.IsActive || pixelsPerSquare <= 0)
            {
                continue;
            }

            // Prefer bright over dim when radii nearly coincide (bright is drawn on top).
            TryPickRing(
                center,
                light.BrightRadius * pixelsPerSquare,
                previewPoint,
                tolerancePreviewPixels,
                LightHandleKind.BrightRing,
                light,
                ref bestScore,
                ref best);

            if (Math.Abs(light.DimRadius - light.BrightRadius) > NearEqualSquares)
            {
                TryPickRing(
                    center,
                    light.DimRadius * pixelsPerSquare,
                    previewPoint,
                    tolerancePreviewPixels,
                    LightHandleKind.DimRing,
                    light,
                    ref bestScore,
                    ref best);
            }
        }

        return best;
    }

    private static int KindPriority(LightHandleKind kind) =>
        kind switch
        {
            LightHandleKind.Origin => 0,
            LightHandleKind.BrightHandle => 1,
            LightHandleKind.DimHandle => 1,
            LightHandleKind.BrightRing => 2,
            LightHandleKind.DimRing => 3,
            _ => 4,
        };

    private static void TryPickPoint(
        MapPoint center,
        MapPoint previewPoint,
        LightHandleKind kind,
        LightSource light,
        double toleranceSquared,
        ref (int Priority, double DistanceSquared) bestScore,
        ref LightHandlePickTarget? best)
    {
        var dx = previewPoint.X - center.X;
        var dy = previewPoint.Y - center.Y;
        var distanceSquared = dx * dx + dy * dy;
        if (distanceSquared > toleranceSquared)
        {
            return;
        }

        Consider(kind, light, distanceSquared, ref bestScore, ref best);
    }

    private static void TryPickRing(
        MapPoint center,
        double radiusPreview,
        MapPoint previewPoint,
        double tolerancePreviewPixels,
        LightHandleKind kind,
        LightSource light,
        ref (int Priority, double DistanceSquared) bestScore,
        ref LightHandlePickTarget? best)
    {
        if (radiusPreview <= 1e-6)
        {
            return;
        }

        var dx = previewPoint.X - center.X;
        var dy = previewPoint.Y - center.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        var ringDistance = Math.Abs(distance - radiusPreview);
        if (ringDistance > tolerancePreviewPixels)
        {
            return;
        }

        Consider(kind, light, ringDistance * ringDistance, ref bestScore, ref best);
    }

    private static void TryPickTick(
        (MapPoint Center, double AngleRadians) pose,
        MapPoint previewPoint,
        LightHandleKind kind,
        LightSource light,
        ref (int Priority, double DistanceSquared) bestScore,
        ref LightHandlePickTarget? best)
    {
        var cos = Math.Cos(pose.AngleRadians);
        var sin = Math.Sin(pose.AngleRadians);
        var localX = (previewPoint.X - pose.Center.X) * cos + (previewPoint.Y - pose.Center.Y) * sin;
        var localY = -(previewPoint.X - pose.Center.X) * sin + (previewPoint.Y - pose.Center.Y) * cos;
        var halfLength = TickHalfLength + TickPickPadding;
        var halfThickness = TickHalfThickness + TickPickPadding;
        if (Math.Abs(localX) > halfLength || Math.Abs(localY) > halfThickness)
        {
            return;
        }

        var distanceSquared = localX * localX + localY * localY;
        Consider(kind, light, distanceSquared, ref bestScore, ref best);
    }

    private static void Consider(
        LightHandleKind kind,
        LightSource light,
        double distanceSquared,
        ref (int Priority, double DistanceSquared) bestScore,
        ref LightHandlePickTarget? best)
    {
        var priority = KindPriority(kind);
        if (priority > bestScore.Priority ||
            (priority == bestScore.Priority && distanceSquared > bestScore.DistanceSquared))
        {
            return;
        }

        bestScore = (priority, distanceSquared);
        best = new LightHandlePickTarget(light, kind);
    }
}
