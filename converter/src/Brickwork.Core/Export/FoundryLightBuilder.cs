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
        var transform = FoundryExportCanvas.FromMap(map).Transform;
        return map.ExportableLights()
            .Select(light =>
            {
                var canvasPoint = transform.SceneToPreview(light.Position);
                return new FoundryLight(
                    light.DisplayName,
                    (int)Math.Round(canvasPoint.X),
                    (int)Math.Round(canvasPoint.Y),
                    Math.Max(0, light.BrightRadius),
                    Math.Max(0, light.DimRadius),
                    LightColors.Normalize(light.Color),
                    LightAnimations.ToFoundryType(light.Animation),
                    Math.Clamp(light.AnimationSpeed, 1, 10),
                    Math.Clamp(light.AnimationIntensity, 1, 10));
            })
            .ToList();
    }
}
