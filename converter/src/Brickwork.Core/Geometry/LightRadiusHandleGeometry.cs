using Brickwork.Core.Models;

namespace Brickwork.Core.Geometry;

public enum LightRadiusHandleKind
{
    Bright,
    Dim,
}

/// <summary>
/// Side handles for editing a light's bright/dim radii when those rings coincide or are hard to grab.
/// Bright sits on +X; dim on −X, at <c>radius * cellSize</c> scene units from the origin.
/// </summary>
public static class LightRadiusHandleGeometry
{
    public static MapPoint BrightHandleScene(LightSource light, double cellSize) =>
        HandleScene(light.Position, light.BrightRadius, cellSize, positiveX: true);

    public static MapPoint DimHandleScene(LightSource light, double cellSize) =>
        HandleScene(light.Position, light.DimRadius, cellSize, positiveX: false);

    public static bool TryGetHandle(
        LightSource light,
        double cellSize,
        LightRadiusHandleKind kind,
        out MapPoint scenePoint)
    {
        scenePoint = kind == LightRadiusHandleKind.Bright
            ? BrightHandleScene(light, cellSize)
            : DimHandleScene(light, cellSize);
        return cellSize > 1e-9;
    }

    /// <summary>Preview pose for an end tick whose long axis is vertical (across the horizontal radius bar).</summary>
    public static (MapPoint Center, double AngleRadians) GetHandleTickPose(
        MapPoint handleScene,
        SceneTransform transform) =>
        (transform.SceneToPreview(handleScene), Math.PI / 2d);

    /// <summary>
    /// Sets bright or dim radius from a scene point's distance to the light origin (in grid squares).
    /// Keeps <see cref="LightSource.DimRadius"/> ≥ <see cref="LightSource.BrightRadius"/>.
    /// </summary>
    public static void SetRadiusFromScene(
        LightSource light,
        double cellSize,
        MapPoint scenePoint,
        LightRadiusHandleKind kind)
    {
        if (cellSize <= 1e-9)
        {
            return;
        }

        var dx = scenePoint.X - light.Position.X;
        var dy = scenePoint.Y - light.Position.Y;
        SetRadiusSquares(light, Math.Sqrt(dx * dx + dy * dy) / cellSize, kind);
    }

    /// <summary>
    /// Sets bright or dim radius to an absolute value in grid squares.
    /// Keeps <see cref="LightSource.DimRadius"/> ≥ <see cref="LightSource.BrightRadius"/>.
    /// </summary>
    public static void SetRadiusSquares(LightSource light, double squares, LightRadiusHandleKind kind)
    {
        squares = Math.Max(0d, squares);
        if (kind == LightRadiusHandleKind.Bright)
        {
            light.BrightRadius = squares;
            if (light.DimRadius < light.BrightRadius)
            {
                light.DimRadius = light.BrightRadius;
            }
        }
        else
        {
            light.DimRadius = Math.Max(squares, light.BrightRadius);
        }
    }

    /// <summary>Sets dim radius from a scene point (used when dragging the dim ring).</summary>
    public static void SetDimRadiusFromScene(LightSource light, double cellSize, MapPoint scenePoint) =>
        SetRadiusFromScene(light, cellSize, scenePoint, LightRadiusHandleKind.Dim);

    private static MapPoint HandleScene(MapPoint origin, double radiusSquares, double cellSize, bool positiveX)
    {
        var offset = radiusSquares * cellSize;
        return new MapPoint(origin.X + (positiveX ? offset : -offset), origin.Y);
    }
}
