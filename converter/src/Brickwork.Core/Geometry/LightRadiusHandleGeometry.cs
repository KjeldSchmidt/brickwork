using Brickwork.Core.Models;

namespace Brickwork.Core.Geometry;

public enum LightRadiusHandleKind
{
    Bright,
    Dim,
}

/// <summary>Updates a light's bright/dim radii from ring-drag gestures.</summary>
public static class LightRadiusHandleGeometry
{
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
}
