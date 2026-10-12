namespace Brickwork.Core.Models;

public sealed class SceneTransform
{
    /// <summary>Minimum interactive padding around the preview image (preview pixels).</summary>
    public const double DefaultPreviewPadding = 64d;

    /// <summary>Extra margin beyond the farthest out-of-bounds overlay point (preview pixels).</summary>
    public const double OverflowMargin = 24d;

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
    /// Preview transform with padding so walls/regions that extend past the image
    /// remain visible and pickable. Export code should keep using <see cref="FromMap"/>.
    /// </summary>
    public static SceneTransform? ForMapPreview(MapDocument map, double minPadding = DefaultPreviewPadding)
    {
        var transform = FromMap(map);
        if (transform is null)
        {
            return null;
        }

        var scaleX = transform.ScaleX;
        var scaleY = transform.ScaleY;
        var minSceneX = 0d;
        var minSceneY = 0d;
        var maxSceneX = transform.SceneWidth;
        var maxSceneY = transform.SceneHeight;
        var hasPoints = false;

        foreach (var wall in map.Walls)
        {
            foreach (var point in wall.Points)
            {
                hasPoints = true;
                minSceneX = Math.Min(minSceneX, point.X);
                minSceneY = Math.Min(minSceneY, point.Y);
                maxSceneX = Math.Max(maxSceneX, point.X);
                maxSceneY = Math.Max(maxSceneY, point.Y);
            }
        }

        foreach (var region in map.Regions)
        {
            foreach (var point in region.Points)
            {
                hasPoints = true;
                minSceneX = Math.Min(minSceneX, point.X);
                minSceneY = Math.Min(minSceneY, point.Y);
                maxSceneX = Math.Max(maxSceneX, point.X);
                maxSceneY = Math.Max(maxSceneY, point.Y);
            }
        }

        var left = minPadding;
        var top = minPadding;
        var right = minPadding;
        var bottom = minPadding;

        if (hasPoints)
        {
            left = Math.Max(left, (-minSceneX * scaleX) + OverflowMargin);
            top = Math.Max(top, (-minSceneY * scaleY) + OverflowMargin);
            right = Math.Max(right, ((maxSceneX - transform.SceneWidth) * scaleX) + OverflowMargin);
            bottom = Math.Max(bottom, ((maxSceneY - transform.SceneHeight) * scaleY) + OverflowMargin);
        }

        return new SceneTransform
        {
            SceneWidth = transform.SceneWidth,
            SceneHeight = transform.SceneHeight,
            PreviewWidth = transform.PreviewWidth,
            PreviewHeight = transform.PreviewHeight,
            OriginOffsetX = left,
            OriginOffsetY = top,
            RightPadding = right,
            BottomPadding = bottom,
        };
    }

    public MapPoint SceneToPreview(MapPoint scenePoint) =>
        new((scenePoint.X * ScaleX) + OriginOffsetX, (scenePoint.Y * ScaleY) + OriginOffsetY);

    public MapPoint PreviewToScene(MapPoint previewPoint) =>
        new((previewPoint.X - OriginOffsetX) / ScaleX, (previewPoint.Y - OriginOffsetY) / ScaleY);
}
