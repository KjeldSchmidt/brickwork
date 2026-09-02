using System.Text.Json;
using Brickwork.Core.Geometry;
using Brickwork.Core.Models;
using SkiaSharp;

namespace Brickwork.Inkarnate.Parsing;

internal static class InkSvgPathParser
{
    private const float CubicSampleStep = 25f;
    private const int CubicMaxSamples = 8;
    private const double PointEpsilon = 0.01;

    public static bool IsClosedPath(string pathData)
    {
        if (string.IsNullOrWhiteSpace(pathData))
        {
            return false;
        }

        using var path = SKPath.ParseSvgPathData(pathData);
        if (path is null || path.IsEmpty)
        {
            return false;
        }

        using var iterator = path.CreateIterator(false);
        var coords = new SKPoint[4];
        while (true)
        {
            var verb = iterator.Next(coords);
            if (verb == SKPathVerb.Done)
            {
                return false;
            }

            if (verb == SKPathVerb.Close)
            {
                return true;
            }
        }
    }

    public static bool ResolveIsClosedPath(JsonElement entity, string pathData)
    {
        if (entity.TryGetProperty("isClosedPath", out var closedElement))
        {
            return closedElement.ValueKind == JsonValueKind.True;
        }

        return IsClosedPath(pathData);
    }

    public static IList<MapPoint> ParseToScenePoints(
        string pathData,
        double originX,
        double originY,
        double scale) =>
        ParseToScenePoints(
            pathData,
            originX,
            originY,
            scale,
            angleDegrees: 0,
            pivotX: originX,
            pivotY: originY);

    public static IList<MapPoint> ParseToScenePoints(
        string pathData,
        double originX,
        double originY,
        double scale,
        double angleDegrees,
        double pivotX,
        double pivotY)
    {
        var localPoints = ParseToLocalPoints(pathData);
        if (localPoints.Count == 0)
        {
            return [];
        }

        var transform = new Wall
        {
            PathOrigin = new MapPoint(originX, originY),
            RotationPivot = new MapPoint(pivotX, pivotY),
            Scale = scale <= 0 ? 1 : scale,
            Angle = angleDegrees,
        };

        return localPoints
            .Select(local => MapPointTransforms.LocalToScene(transform, local))
            .ToList();
    }

    public static IList<MapPoint> ParseToLocalPoints(string pathData)
    {
        if (string.IsNullOrWhiteSpace(pathData))
        {
            return [];
        }

        using var path = SKPath.ParseSvgPathData(pathData);
        if (path is null || path.IsEmpty)
        {
            return [];
        }

        var points = new List<MapPoint>();
        using var iterator = path.CreateIterator(false);
        var coords = new SKPoint[4];
        var current = default(SKPoint);
        var hasCurrent = false;

        while (true)
        {
            var verb = iterator.Next(coords);
            switch (verb)
            {
                case SKPathVerb.Done:
                    RemoveClosingDuplicate(points);
                    return points;
                case SKPathVerb.Move:
                    current = coords[0];
                    hasCurrent = true;
                    TryAddLocalPoint(points, current);
                    break;
                case SKPathVerb.Line:
                    if (!hasCurrent)
                    {
                        break;
                    }

                    current = coords[1];
                    TryAddLocalPoint(points, current);
                    break;
                case SKPathVerb.Quad:
                    if (!hasCurrent)
                    {
                        break;
                    }

                    SampleCurve(points, coords[0], coords[1], coords[2], quad: true);
                    current = coords[2];
                    break;
                case SKPathVerb.Conic:
                    if (!hasCurrent)
                    {
                        break;
                    }

                    SampleCurve(
                        points,
                        coords[0],
                        coords[1],
                        coords[2],
                        conicWeight: iterator.ConicWeight());
                    current = coords[2];
                    break;
                case SKPathVerb.Cubic:
                    if (!hasCurrent)
                    {
                        break;
                    }

                    SampleCubic(points, coords[0], coords[1], coords[2], coords[3]);
                    current = coords[3];
                    break;
                case SKPathVerb.Close:
                    break;
            }
        }
    }

    private static void RemoveClosingDuplicate(List<MapPoint> points)
    {
        if (points.Count >= 2 && NearlyEqual(points[0], points[^1]))
        {
            points.RemoveAt(points.Count - 1);
        }
    }

    private static void SampleCubic(
        List<MapPoint> points,
        SKPoint start,
        SKPoint control1,
        SKPoint control2,
        SKPoint end)
    {
        using var segment = new SKPath();
        segment.MoveTo(start);
        segment.CubicTo(control1, control2, end);
        SamplePathSegment(points, segment, end);
    }

    private static void SampleCurve(
        List<MapPoint> points,
        SKPoint start,
        SKPoint control,
        SKPoint end,
        bool quad = false,
        float conicWeight = 0f)
    {
        using var segment = new SKPath();
        segment.MoveTo(start);
        if (quad)
        {
            segment.QuadTo(control, end);
        }
        else
        {
            segment.ConicTo(control, end, conicWeight);
        }

        SamplePathSegment(points, segment, end);
    }

    private static void SamplePathSegment(List<MapPoint> points, SKPath segment, SKPoint end)
    {
        using var measure = new SKPathMeasure(segment, false);
        var length = measure.Length;
        if (length <= 0)
        {
            TryAddLocalPoint(points, end);
            return;
        }

        var step = Math.Min(CubicSampleStep, length / CubicMaxSamples);
        if (step < 1f)
        {
            step = 1f;
        }

        for (var distance = step; distance < length; distance += step)
        {
            if (measure.GetPosition(distance, out var position))
            {
                TryAddLocalPoint(points, position);
            }
        }

        TryAddLocalPoint(points, end);
    }

    private static void TryAddLocalPoint(List<MapPoint> points, SKPoint local)
    {
        var point = new MapPoint(local.X, local.Y);
        if (points.Count > 0 && NearlyEqual(points[^1], point))
        {
            return;
        }

        points.Add(point);
    }

    private static bool NearlyEqual(MapPoint a, MapPoint b) =>
        Math.Abs(a.X - b.X) < PointEpsilon && Math.Abs(a.Y - b.Y) < PointEpsilon;
}
