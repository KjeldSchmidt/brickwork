using InkarnateTools.Core.Geometry;
using InkarnateTools.Core.Models;
using SkiaSharp;

namespace InkarnateTools.App.Rendering;

public sealed class MapSceneRenderer : IMapSceneRenderer
{
    private const float LineStrokeWidth = 2f;

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
                DrawWallSegment(
                    canvas,
                    transform,
                    segment,
                    wall.LineType,
                    wall.IsActive,
                    wall.SceneThickness,
                    wall.IsClosed);
            }

            foreach (var portalSegment in WallPathSegmentBuilder.BuildPortalSegments(wall))
            {
                DrawWallSegment(
                    canvas,
                    transform,
                    portalSegment.Points,
                    portalSegment.Portal.LineType,
                    portalSegment.Portal.IsActive,
                    wall.SceneThickness,
                    isClosed: false);
            }

            DrawCenterlineNodes(canvas, transform, wall.Points, wall.IsActive);
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

    private static void DrawWallSegment(
        SKCanvas canvas,
        SceneTransform transform,
        IReadOnlyList<MapPoint> scenePoints,
        WallLineType lineType,
        bool isActive,
        double sceneThickness,
        bool isClosed)
    {
        if (scenePoints.Count < 2)
        {
            return;
        }

        if (lineType == WallLineType.Terrain && sceneThickness > 0)
        {
            if (isClosed)
            {
                var ring = WallThicknessPolygonBuilder.BuildClosedRing(scenePoints, sceneThickness);
                if (ring is not null && ring.Outer.Count >= 3 && ring.Inner.Count >= 3)
                {
                    DrawTerrainRing(
                        canvas,
                        transform,
                        ring,
                        WallLineColors.FillForLine(lineType, isActive),
                        WallLineColors.ForLine(lineType, isActive));
                    return;
                }
            }
            else
            {
                var outline = WallThicknessPolygonBuilder.BuildOutline(scenePoints, sceneThickness);
                if (outline.Count >= 3)
                {
                    DrawPolygon(
                        canvas,
                        transform,
                        outline,
                        WallLineColors.FillForLine(lineType, isActive),
                        WallLineColors.ForLine(lineType, isActive));
                    return;
                }
            }
        }

        DrawPolyline(canvas, transform, scenePoints, WallLineColors.ForLine(lineType, isActive), isClosed);
    }

    private static void DrawTerrainRing(
        SKCanvas canvas,
        SceneTransform transform,
        WallTerrainRing ring,
        SKColor fillColor,
        SKColor strokeColor)
    {
        using var path = new SKPath { FillType = SKPathFillType.EvenOdd };
        AddClosedLoop(path, transform, ring.Outer);
        AddClosedLoop(path, transform, ring.Inner);

        using var fillPaint = new SKPaint
        {
            Color = fillColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawPath(path, fillPaint);

        using var strokePaint = new SKPaint
        {
            Color = strokeColor,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = LineStrokeWidth,
            StrokeJoin = SKStrokeJoin.Round,
        };
        canvas.DrawPath(path, strokePaint);
    }

    private static void AddClosedLoop(SKPath path, SceneTransform transform, IReadOnlyList<MapPoint> loop)
    {
        if (loop.Count < 3)
        {
            return;
        }

        var first = transform.SceneToPreview(loop[0]);
        path.MoveTo((float)first.X, (float)first.Y);

        for (var i = 1; i < loop.Count; i++)
        {
            var point = transform.SceneToPreview(loop[i]);
            path.LineTo((float)point.X, (float)point.Y);
        }

        path.Close();
    }

    private static void DrawPolygon(
        SKCanvas canvas,
        SceneTransform transform,
        IReadOnlyList<MapPoint> scenePoints,
        SKColor fillColor,
        SKColor strokeColor)
    {
        using var path = BuildPath(transform, scenePoints);
        path.Close();

        using var fillPaint = new SKPaint
        {
            Color = fillColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawPath(path, fillPaint);

        using var strokePaint = new SKPaint
        {
            Color = strokeColor,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = LineStrokeWidth,
            StrokeJoin = SKStrokeJoin.Round,
        };
        canvas.DrawPath(path, strokePaint);
    }

    private static void DrawPolyline(
        SKCanvas canvas,
        SceneTransform transform,
        IReadOnlyList<MapPoint> scenePoints,
        SKColor color,
        bool isClosed)
    {
        using var path = BuildPath(transform, scenePoints);
        if (isClosed)
        {
            path.Close();
        }

        using var paint = new SKPaint
        {
            Color = color,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = LineStrokeWidth,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeCap = SKStrokeCap.Round,
        };

        canvas.DrawPath(path, paint);
    }

    private static void DrawCenterlineNodes(
        SKCanvas canvas,
        SceneTransform transform,
        IList<MapPoint> scenePoints,
        bool isActive)
    {
        // Tessellation produces ~1 point per scene unit (thousands per wall).
        // Large outlined circles overlap into a solid black band — use tiny dots instead.
        for (var i = 0; i < scenePoints.Count; i++)
        {
            var preview = transform.SceneToPreview(scenePoints[i]);
            var x = (float)preview.X;
            var y = (float)preview.Y;

            if (i == 0)
            {
                using var startPaint = new SKPaint
                {
                    Color = SKColors.Lime,
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill,
                };
                canvas.DrawCircle(x, y, 4f, startPaint);
                continue;
            }

            var fillColor = isActive
                ? new SKColor(0xFF, 0xFF, 0xFF, 0xCC)
                : new SKColor(0xAA, 0xAA, 0xAA, 0xCC);

            using var fillPaint = new SKPaint
            {
                Color = fillColor,
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
            };
            canvas.DrawCircle(x, y, 1f, fillPaint);
        }
    }

    private static SKPath BuildPath(SceneTransform transform, IReadOnlyList<MapPoint> scenePoints)
    {
        var path = new SKPath();
        var first = transform.SceneToPreview(scenePoints[0]);
        path.MoveTo((float)first.X, (float)first.Y);

        for (var i = 1; i < scenePoints.Count; i++)
        {
            var point = transform.SceneToPreview(scenePoints[i]);
            path.LineTo((float)point.X, (float)point.Y);
        }

        return path;
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
