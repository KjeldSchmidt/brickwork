namespace Brickwork.Core.Models;

public sealed class SceneTransform
{
    /// <summary>Fixed interactive padding as a fraction of preview width/height on each side.</summary>
    public const double PreviewPaddingFraction = 0.10d;

    public double SceneWidth { get; init; }

    public double SceneHeight { get; init; }

    public double PreviewWidth { get; init; }

    public double PreviewHeight { get; init; }

    /// <summary>Left padding in preview pixels so overlays can sit outside the image.</summary>
    public double OriginOffsetX { get; init; }

    /// <summary>Top padding in preview pixels so overlays can sit outside the image.</summary>
    public double OriginOffsetY { get; init; }

    /// <summary>Right padding in preview pixels beyond the image width.</summary>
    public double RightPadding { get; init; }

    /// <summary>Bottom padding in preview pixels beyond the image height.</summary>
    public double BottomPadding { get; init; }

    public double ScaleX => SceneWidth > 0 ? PreviewWidth / SceneWidth : 1d;

    public double ScaleY => SceneHeight > 0 ? PreviewHeight / SceneHeight : 1d;

    /// <summary>Full interactive canvas size including overflow padding.</summary>
    public double CanvasWidth => PreviewWidth + OriginOffsetX + RightPadding;

    /// <summary>Full interactive canvas size including overflow padding.</summary>
    public double CanvasHeight => PreviewHeight + OriginOffsetY + BottomPadding;

    public static SceneTransform? FromMap(MapDocument map)
    {
        if (map.Preview is not { Width: > 0, Height: > 0 } preview ||
            map.Scene is not { Width: > 0, Height: > 0 } scene)
        {
            return null;
        }

        return new SceneTransform
        {
            SceneWidth = scene.Width,
            SceneHeight = scene.Height,
            PreviewWidth = preview.Width,
            PreviewHeight = preview.Height,
        };
    }

    /// <summary>
    /// Preview transform with fixed 10% padding so walls/regions that extend slightly
    /// past the image remain visible and pickable. Export code should keep using <see cref="FromMap"/>.
    /// </summary>
    public static SceneTransform? ForMapPreview(MapDocument map)
    {
        var transform = FromMap(map);
        if (transform is null)
        {
            return null;
        }

        var padX = transform.PreviewWidth * PreviewPaddingFraction;
        var padY = transform.PreviewHeight * PreviewPaddingFraction;

        return new SceneTransform
        {
            SceneWidth = transform.SceneWidth,
            SceneHeight = transform.SceneHeight,
            PreviewWidth = transform.PreviewWidth,
            PreviewHeight = transform.PreviewHeight,
            OriginOffsetX = padX,
            OriginOffsetY = padY,
            RightPadding = padX,
            BottomPadding = padY,
        };
    }

    public MapPoint SceneToPreview(MapPoint scenePoint) =>
        new((scenePoint.X * ScaleX) + OriginOffsetX, (scenePoint.Y * ScaleY) + OriginOffsetY);

    public MapPoint PreviewToScene(MapPoint previewPoint) =>
        new((previewPoint.X - OriginOffsetX) / ScaleX, (previewPoint.Y - OriginOffsetY) / ScaleY);
}
