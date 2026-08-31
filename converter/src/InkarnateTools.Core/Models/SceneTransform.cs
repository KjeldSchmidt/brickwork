namespace InkarnateTools.Core.Models;

public sealed class SceneTransform
{
    public double SceneWidth { get; init; }

    public double SceneHeight { get; init; }

    public double PreviewWidth { get; init; }

    public double PreviewHeight { get; init; }

    public double ScaleX => SceneWidth > 0 ? PreviewWidth / SceneWidth : 1d;

    public double ScaleY => SceneHeight > 0 ? PreviewHeight / SceneHeight : 1d;

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

    public MapPoint SceneToPreview(MapPoint scenePoint) =>
        new(scenePoint.X * ScaleX, scenePoint.Y * ScaleY);

    public MapPoint PreviewToScene(MapPoint previewPoint) =>
        new(previewPoint.X / ScaleX, previewPoint.Y / ScaleY);
}
