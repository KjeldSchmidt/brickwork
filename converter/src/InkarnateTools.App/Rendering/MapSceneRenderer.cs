using InkarnateTools.Core.Geometry;
using InkarnateTools.Core.Models;
using SkiaSharp;

namespace InkarnateTools.App.Rendering;

public sealed class MapSceneRenderer : IMapSceneRenderer
{
    private const float LineStrokeWidth = 2f;
    private const float NodeRadius = 4f;
    private const float NodeBorderWidth = 1.5f;
    private const float TickHalfLength = 4f;
    private const float TickHalfThickness = 2f;

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
                    segment.Points,
                    wall.LineType,
                    wall.IsActive,
                    wall.SceneThickness,
                    segment.IsClosed);
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

            DrawWallNodes(canvas, transform, wall);
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

    private static void DrawWallNodes(SKCanvas canvas, SceneTransform transform, Wall wall)
    {
        for (var i = 0; i < wall.Points.Count; i++)
        {
            DrawWallNode(canvas, transform, wall.Points[i], wall.LineType, wall.IsActive);
        }

        foreach (var portal in wall.Portals)
        {
            if (WallPathSegmentBuilder.TryGetPortalArcInterval(wall, portal, out var start, out var end))
            {
                DrawPortalWidthTick(canvas, transform, wall, start, portal.LineType, portal.IsActive);
                DrawPortalWidthTick(canvas, transform, wall, end, portal.LineType, portal.IsActive);
            }

            var anchorScene = WallPathSegmentBuilder.PortalAnchorToScene(wall, portal);
            DrawWallNode(canvas, transform, anchorScene, portal.LineType, portal.IsActive);
        }
    }

    private static void DrawPortalWidthTick(
        SKCanvas canvas,
        SceneTransform transform,
        Wall wall,
        double arcLength,
        WallLineType lineType,
        bool isActive)
    {
        var (center, angleRadians) = PortalWidthHandleGeometry.GetPreviewTickPose(wall, arcLength, transform);
        var degrees = (float)(angleRadians * 180d / Math.PI);

        canvas.Save();
        canvas.Translate((float)center.X, (float)center.Y);
        canvas.RotateDegrees(degrees);

        var rect = new SKRect(
            -TickHalfLength,
            -TickHalfThickness,
            TickHalfLength,
            TickHalfThickness);

        var fillColor = isActive
            ? new SKColor(0xFF, 0xFF, 0xFF, 0xCC)
            : new SKColor(0xAA, 0xAA, 0xAA, 0xCC);

        using var fillPaint = new SKPaint
        {
            Color = fillColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawRect(rect, fillPaint);

        using var borderPaint = new SKPaint
        {
            Color = WallLineColors.ForLine(lineType, isActive),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = NodeBorderWidth,
        };
        canvas.DrawRect(rect, borderPaint);

        canvas.Restore();
    }

    private static void DrawWallNode(
        SKCanvas canvas,
        SceneTransform transform,
        MapPoint scenePoint,
        WallLineType lineType,
        bool isActive)
    {
        var preview = transform.SceneToPreview(scenePoint);
        var x = (float)preview.X;
        var y = (float)preview.Y;

        var fillColor = isActive
            ? new SKColor(0xFF, 0xFF, 0xFF, 0xCC)
            : new SKColor(0xAA, 0xAA, 0xAA, 0xCC);

        using var fillPaint = new SKPaint
        {
            Color = fillColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawCircle(x, y, NodeRadius, fillPaint);

        using var borderPaint = new SKPaint
        {
            Color = WallLineColors.ForLine(lineType, isActive),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = NodeBorderWidth,
        };
        canvas.DrawCircle(x, y, NodeRadius, borderPaint);
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
