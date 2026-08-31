using InkarnateTools.Core.Models;
using SkiaSharp;

namespace InkarnateTools.App.Rendering;

public sealed class MapSceneRenderer : IMapSceneRenderer
{
    private readonly Dictionary<MapDocument, SKImage> _imageCache = new(ReferenceEqualityComparer.Instance);

    public void Render(SKCanvas canvas, MapDocument map, SKRect destinationBounds)
    {
        canvas.Clear(SKColors.Black);

        var previewImage = GetOrCreateImage(map);
        if (previewImage is not null)
        {
            canvas.DrawImage(previewImage, destinationBounds);
        }

        var transform = SceneTransform.FromMap(map);
        if (transform is null)
        {
            return;
        }

        using var wallPaint = new SKPaint
        {
            Color = new SKColor(0xFF, 0x44, 0x44, 0xCC),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
        };

        foreach (var wall in map.Walls)
        {
            if (wall.Points.Count < 2)
            {
                continue;
            }

            using var path = new SKPath();
            var first = transform.SceneToPreview(wall.Points[0]);
            path.MoveTo((float)first.X, (float)first.Y);

            for (var i = 1; i < wall.Points.Count; i++)
            {
                var point = transform.SceneToPreview(wall.Points[i]);
                path.LineTo((float)point.X, (float)point.Y);
            }

            canvas.DrawPath(path, wallPaint);
        }
    }

    public void Invalidate(MapDocument? map)
    {
        if (map is null)
        {
            return;
        }

        if (_imageCache.Remove(map, out var image))
        {
            image.Dispose();
        }
    }

    public void ClearCache()
    {
        foreach (var image in _imageCache.Values)
        {
            image.Dispose();
        }

        _imageCache.Clear();
    }

    private SKImage? GetOrCreateImage(MapDocument map)
    {
        if (_imageCache.TryGetValue(map, out var cached))
        {
            return cached;
        }

        if (map.PreviewImagePng is not { Length: > 0 } pngBytes)
        {
            return null;
        }

        using var data = SKData.CreateCopy(pngBytes);
        var image = SKImage.FromEncodedData(data);
        if (image is not null)
        {
            _imageCache[map] = image;
        }

        return image;
    }
}
