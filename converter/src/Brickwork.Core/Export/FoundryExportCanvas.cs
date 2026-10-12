using Brickwork.Core.Models;

namespace Brickwork.Core.Export;

/// <summary>
/// Pixel canvas used for Foundry scene dimensions and wall/region coordinates.
/// </summary>
public sealed class FoundryExportCanvas
{
    public const int FallbackGridSize = 100;

    public int Width { get; init; }

    public int Height { get; init; }

    /// <summary>Foundry <c>grid.size</c> to write, or null to leave the scene template value.</summary>
    public int? GridSize { get; init; }

    public SceneTransform Transform { get; init; } = null!;

    public static FoundryExportCanvas FromMap(MapDocument map)
    {
        ArgumentNullException.ThrowIfNull(map);

        if (map.Scene is not { Width: > 0, Height: > 0 } scene)
        {
            throw new InvalidOperationException("Map scene dimensions are required for Foundry export.");
        }

        if (map.UnderlyingMap is { IsConnected: true, Width: > 0, Height: > 0 } underlying &&
            map.Grid is { Columns: > 0, Rows: > 0 })
        {
            var gridSize = Math.Max(1, (int)Math.Round(underlying.Width / (double)map.Grid.Columns));
            return Create(scene, underlying.Width, underlying.Height, gridSize);
        }

        if (map.Grid is { Columns: > 0, Rows: > 0 })
        {
            var width = map.Grid.Columns * FallbackGridSize;
            var height = map.Grid.Rows * FallbackGridSize;
            return Create(scene, width, height, FallbackGridSize);
        }

        if (map.Preview is { Width: > 0, Height: > 0 } preview)
        {
            return Create(scene, preview.Width, preview.Height, gridSize: null);
        }

        throw new InvalidOperationException(
            "Foundry export requires an underlying Foundry map, grid metrics, or preview dimensions.");
    }

    private static FoundryExportCanvas Create(
        SceneDimensions scene,
        int canvasWidth,
        int canvasHeight,
        int? gridSize) =>
        new()
        {
            Width = canvasWidth,
            Height = canvasHeight,
            GridSize = gridSize,
            Transform = new SceneTransform
            {
                SceneWidth = scene.Width,
                SceneHeight = scene.Height,
                PreviewWidth = canvasWidth,
                PreviewHeight = canvasHeight,
            },
        };
}
