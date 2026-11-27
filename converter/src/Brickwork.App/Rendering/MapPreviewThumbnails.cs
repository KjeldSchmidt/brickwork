using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using Brickwork.Core.Models;
using SkiaSharp;

namespace Brickwork.App.Rendering;

/// <summary>Cuts small thumbnails out of a map's embedded preview image, e.g. to show what a tree version looks like.</summary>
public static class MapPreviewThumbnails
{
    private const int ThumbnailPixels = 48;

    private static readonly ConditionalWeakTable<MapDocument, SKBitmap?> Decoded = new();

    /// <summary>
    /// The preview around <paramref name="center"/> (scene units), <paramref name="sceneSize"/>
    /// wide and high, or null when the map has no preview.
    /// </summary>
    public static Bitmap? Create(MapDocument map, MapPoint center, double sceneSize)
    {
        var transform = SceneTransform.FromMap(map);
        var preview = Decoded.GetValue(map, Decode);
        if (transform is null || preview is null || transform.PreviewWidth <= 0 || sceneSize <= 0)
        {
            return null;
        }

        var previewPoint = transform.SceneToPreview(center);
        var pixelsPerPreview = preview.Width / transform.PreviewWidth;
        var half = sceneSize * transform.ScaleX * pixelsPerPreview / 2d;
        var bounds = SKRectI.Round(new SKRect(
            (float)(previewPoint.X * pixelsPerPreview - half),
            (float)(previewPoint.Y * pixelsPerPreview - half),
            (float)(previewPoint.X * pixelsPerPreview + half),
            (float)(previewPoint.Y * pixelsPerPreview + half)));
        bounds.Intersect(new SKRectI(0, 0, preview.Width, preview.Height));
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return null;
        }

        using var crop = new SKBitmap(ThumbnailPixels, ThumbnailPixels);
        using (var canvas = new SKCanvas(crop))
        using (var paint = new SKPaint { FilterQuality = SKFilterQuality.Medium })
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(preview, bounds, new SKRect(0, 0, ThumbnailPixels, ThumbnailPixels), paint);
        }

        using var image = SKImage.FromBitmap(crop);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(data.ToArray());
        return new Bitmap(stream);
    }

    private static SKBitmap? Decode(MapDocument map) =>
        map.PreviewImagePng is { Length: > 0 } png ? SKBitmap.Decode(png) : null;
}
