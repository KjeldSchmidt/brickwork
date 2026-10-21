using Brickwork.Core.Models;

namespace Brickwork.Core.Export;

public sealed record FoundryLight(
    string Name,
    int X,
    int Y,
    double BrightSquares,
    double DimSquares,
    string Color,
    string? AnimationType,
    int AnimationSpeed,
    int AnimationIntensity);

/// <summary>Maps active lights to Foundry canvas coordinates.</summary>
public static class FoundryLightBuilder
{
    public static IReadOnlyList<FoundryLight> BuildFromMap(MapDocument map)
    {
        var canvas = FoundryExportCanvas.FromMap(map);
        var transform = canvas.Transform;
        var squares = canvas.SquaresPerMapCell;
        return map.ExportableLights()
            .Select(light =>
            {
                var canvasPoint = transform.SceneToPreview(light.Position);
                return new FoundryLight(
                    light.DisplayName,
                    (int)Math.Round(canvasPoint.X),
                    (int)Math.Round(canvasPoint.Y),
                    Math.Max(0, light.BrightRadius) * squares,
                    Math.Max(0, light.DimRadius) * squares,
                    LightColors.Normalize(light.Color),
                    LightAnimations.ToFoundryType(light.Animation),
                    Math.Clamp(light.AnimationSpeed, 1, 10),
                    Math.Clamp(light.AnimationIntensity, 1, 10));
            })
            .ToList();
    }
}
