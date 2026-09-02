using Brickwork.Core.Geometry;
using Brickwork.Core.Models;
using SkiaSharp;

namespace Brickwork.App.Rendering;

public sealed class MapSceneRenderer : IMapSceneRenderer
{
    private const float LineStrokeWidth = 2f;
    private const float NodeRadius = 4f;
    private const float NodeBorderWidth = 1.5f;
    private const float TickHalfLength = 4f;
    private const float TickHalfThickness = 2f;

    private readonly Dictionary<MapDocument, SKImage> _imageCache = new(ReferenceEqualityComparer.Instance);

    public void Render(SKCanvas canvas, MapDocument map, SKRect destinationBounds, MapRenderHighlight? highlight = null)
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

        if (highlight?.ActiveTarget is not { } target)
        {
            return;
        }

        DrawWallHighlight(canvas, map, transform, target.WallEntityId, target.Portal);
    }

    public void ReleaseMap(MapDocument map) => _imageCache.Remove(map);

    private static void DrawWallHighlight(
        SKCanvas canvas,
        MapDocument map,
        SceneTransform transform,
        int wallEntityId,
        WallPortal? portal)
    {
        var wall = map.Walls.FirstOrDefault(candidate => candidate.EntityId == wallEntityId);
        if (wall is null || !wall.WallEnabled || wall.Points.Count < 2)
        {
            return;
        }

        var color = WallLineColors.ForHighlight();
        var underlayWidth = LineStrokeWidth * 2f;

        DrawWallHighlightOverlay(
            canvas,
            transform,
            wall,
            portal,
            color,
            underlayWidth,
            nodeRadius: NodeRadius + 1.5f);

        RedrawCoreWallGeometry(canvas, transform, wall, portal);
    }

    private static void DrawWallHighlightOverlay(
        SKCanvas canvas,
        SceneTransform transform,
        Wall wall,
        WallPortal? portal,
        SKColor color,
        float strokeWidth,
        float nodeRadius)
    {
        foreach (var segment in WallPathSegmentBuilder.BuildSegments(wall))
        {
            if (wall.LineType == WallLineType.Terrain && wall.SceneThickness > 0)
            {
                DrawTerrainHighlightStroke(
                    canvas,
                    transform,
                    segment.Points,
                    wall.SceneThickness,
                    segment.IsClosed,
                    color,
                    strokeWidth);
            }
            else
            {
                DrawPolyline(canvas, transform, segment.Points, color, segment.IsClosed, strokeWidth);
            }
        }

        if (portal is null)
        {
            foreach (var portalSegment in WallPathSegmentBuilder.BuildPortalSegments(wall))
            {
                DrawPolyline(canvas, transform, portalSegment.Points, color, isClosed: false, strokeWidth);
            }

            DrawWallNodes(canvas, transform, wall, color, nodeRadius, strokeWidth);
            return;
        }

        foreach (var portalSegment in WallPathSegmentBuilder.BuildPortalSegments(wall))
        {
            if (ReferenceEquals(portalSegment.Portal, portal))
            {
                DrawPolyline(canvas, transform, portalSegment.Points, color, isClosed: false, strokeWidth);
            }
        }

        if (WallPathSegmentBuilder.TryGetPortalArcInterval(wall, portal, out var start, out var end))
        {
            DrawPortalWidthTick(canvas, transform, wall, start, portal.LineType, portal.IsActive, color, strokeWidth);
            DrawPortalWidthTick(canvas, transform, wall, end, portal.LineType, portal.IsActive, color, strokeWidth);
        }

        var anchorScene = WallPathSegmentBuilder.PortalAnchorToScene(wall, portal);
        DrawWallNode(canvas, transform, anchorScene, portal.LineType, portal.IsActive, color, nodeRadius, strokeWidth);
    }

    private static void RedrawCoreWallGeometry(
        SKCanvas canvas,
        SceneTransform transform,
        Wall wall,
        WallPortal? portal)
    {
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

        if (portal is null)
        {
            DrawWallNodes(canvas, transform, wall);
            return;
        }

        if (WallPathSegmentBuilder.TryGetPortalArcInterval(wall, portal, out var start, out var end))
        {
            DrawPortalWidthTick(canvas, transform, wall, start, portal.LineType, portal.IsActive);
            DrawPortalWidthTick(canvas, transform, wall, end, portal.LineType, portal.IsActive);
        }

        var anchorScene = WallPathSegmentBuilder.PortalAnchorToScene(wall, portal);
        DrawWallNode(canvas, transform, anchorScene, portal.LineType, portal.IsActive);
    }

    private static void DrawTerrainHighlightStroke(
        SKCanvas canvas,
        SceneTransform transform,
        IReadOnlyList<MapPoint> scenePoints,
        double sceneThickness,
        bool isClosed,
        SKColor color,
        float strokeWidth)
    {
        var loops = WallThicknessPolygonBuilder.BuildTerrainExportLoops(
            scenePoints as IList<MapPoint> ?? scenePoints.ToList(),
            sceneThickness,
            isClosed);

        foreach (var loop in loops)
        {
            DrawPolyline(canvas, transform, loop, color, isClosed: true, strokeWidth);
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
        bool isClosed,
        float strokeWidth = LineStrokeWidth)
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
            StrokeWidth = strokeWidth,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeCap = SKStrokeCap.Round,
        };

        canvas.DrawPath(path, paint);
    }

    private static void DrawWallNodes(
        SKCanvas canvas,
        SceneTransform transform,
        Wall wall,
        SKColor? overrideColor = null,
        float? overrideRadius = null,
        float? overrideBorderWidth = null)
    {
        for (var i = 0; i < wall.Points.Count; i++)
        {
            DrawWallNode(
                canvas,
                transform,
                wall.Points[i],
                wall.LineType,
                wall.IsActive,
                overrideColor,
                overrideRadius,
                overrideBorderWidth);
        }

        foreach (var portal in wall.Portals)
        {
            if (WallPathSegmentBuilder.TryGetPortalArcInterval(wall, portal, out var start, out var end))
            {
                DrawPortalWidthTick(
                    canvas,
                    transform,
                    wall,
                    start,
                    portal.LineType,
                    portal.IsActive,
                    overrideColor,
                    overrideBorderWidth);
                DrawPortalWidthTick(
                    canvas,
                    transform,
                    wall,
                    end,
                    portal.LineType,
                    portal.IsActive,
                    overrideColor,
                    overrideBorderWidth);
            }

            var anchorScene = WallPathSegmentBuilder.PortalAnchorToScene(wall, portal);
            DrawWallNode(
                canvas,
                transform,
                anchorScene,
                portal.LineType,
                portal.IsActive,
                overrideColor,
                overrideRadius,
                overrideBorderWidth);
        }
    }

    private static void DrawPortalWidthTick(
        SKCanvas canvas,
        SceneTransform transform,
        Wall wall,
        double arcLength,
        WallLineType lineType,
        bool isActive,
        SKColor? overrideColor = null,
        float? overrideBorderWidth = null)
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
            Color = overrideColor ?? WallLineColors.ForLine(lineType, isActive),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = overrideBorderWidth ?? NodeBorderWidth,
        };
        canvas.DrawRect(rect, borderPaint);

        canvas.Restore();
    }

    private static void DrawWallNode(
        SKCanvas canvas,
        SceneTransform transform,
        MapPoint scenePoint,
        WallLineType lineType,
        bool isActive,
        SKColor? overrideColor = null,
        float? overrideRadius = null,
        float? overrideBorderWidth = null)
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
        canvas.DrawCircle(x, y, overrideRadius ?? NodeRadius, fillPaint);

        using var borderPaint = new SKPaint
        {
            Color = overrideColor ?? WallLineColors.ForLine(lineType, isActive),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = overrideBorderWidth ?? NodeBorderWidth,
        };
        canvas.DrawCircle(x, y, overrideRadius ?? NodeRadius, borderPaint);
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
