using InkarnateTools.Core.Geometry;
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

        foreach (var wall in map.Walls)
        {
            if (!wall.WallEnabled || wall.Points.Count < 2)
            {
                continue;
            }

            foreach (var segment in WallPathSegmentBuilder.BuildSegments(wall))
            {
                DrawPolyline(
                    canvas,
                    transform,
                    segment,
                    WallLineColors.ForLine(wall.LineType, wall.IsActive));
            }

            foreach (var portalSegment in WallPathSegmentBuilder.BuildPortalSegments(wall))
            {
                DrawPolyline(
                    canvas,
                    transform,
                    portalSegment.Points,
                    WallLineColors.ForLine(portalSegment.Portal.LineType, portalSegment.Portal.IsActive));
            }
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

    private static void DrawPolyline(
        SKCanvas canvas,
        SceneTransform transform,
        IReadOnlyList<MapPoint> scenePoints,
        SKColor color)
    {
        if (scenePoints.Count < 2)
        {
            return;
        }

        using var paint = new SKPaint
        {
            Color = color,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
        };

        using var path = new SKPath();
        var first = transform.SceneToPreview(scenePoints[0]);
        path.MoveTo((float)first.X, (float)first.Y);

        for (var i = 1; i < scenePoints.Count; i++)
        {
            var point = transform.SceneToPreview(scenePoints[i]);
            path.LineTo((float)point.X, (float)point.Y);
        }

        canvas.DrawPath(path, paint);
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
