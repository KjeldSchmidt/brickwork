using InkarnateTools.Core.Models;
using SkiaSharp;

namespace InkarnateTools.Inkarnate.Parsing;

internal static class InkSvgPathParser
{
    private const float CubicSampleStep = 25f;
    private const int CubicMaxSamples = 8;
    private const double PointEpsilon = 0.01;

    public static IList<MapPoint> ParseToScenePoints(
        string pathData,
        double originX,
        double originY,
        double scale)
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
                    TryAddPoint(points, current, originX, originY, scale);
                    break;
                case SKPathVerb.Line:
                    if (!hasCurrent)
                    {
                        break;
                    }

                    current = coords[1];
                    TryAddPoint(points, current, originX, originY, scale);
                    break;
                case SKPathVerb.Quad:
                    if (!hasCurrent)
                    {
                        break;
                    }

                    SampleCurve(
                        points,
                        coords[0],
                        coords[1],
                        coords[2],
                        originX,
                        originY,
                        scale,
                        quad: true);
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
                        originX,
                        originY,
                        scale,
                        conicWeight: iterator.ConicWeight());
                    current = coords[2];
                    break;
                case SKPathVerb.Cubic:
                    if (!hasCurrent)
                    {
                        break;
                    }

                    SampleCubic(
                        points,
                        coords[0],
                        coords[1],
                        coords[2],
                        coords[3],
                        originX,
                        originY,
                        scale);
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
        SKPoint end,
        double originX,
        double originY,
        double scale)
    {
        using var segment = new SKPath();
        segment.MoveTo(start);
        segment.CubicTo(control1, control2, end);

        SamplePathSegment(points, segment, end, originX, originY, scale);
    }

    private static void SampleCurve(
        List<MapPoint> points,
        SKPoint start,
        SKPoint control,
        SKPoint end,
        double originX,
        double originY,
        double scale,
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

        SamplePathSegment(points, segment, end, originX, originY, scale);
    }

    private static void SamplePathSegment(
        List<MapPoint> points,
        SKPath segment,
        SKPoint end,
        double originX,
        double originY,
        double scale)
    {
        using var measure = new SKPathMeasure(segment, false);
        var length = measure.Length;
        if (length <= 0)
        {
            TryAddPoint(points, end, originX, originY, scale);
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
                TryAddPoint(points, position, originX, originY, scale);
            }
        }

        TryAddPoint(points, end, originX, originY, scale);
    }

    private static void TryAddPoint(
        List<MapPoint> points,
        SKPoint local,
        double originX,
        double originY,
        double scale)
    {
        var scenePoint = TransformPoint(local, originX, originY, scale);
        if (points.Count > 0 && NearlyEqual(points[^1], scenePoint))
        {
            return;
        }

        points.Add(scenePoint);
    }

    private static MapPoint TransformPoint(SKPoint local, double originX, double originY, double scale) =>
        new(local.X * scale + originX, local.Y * scale + originY);

    private static bool NearlyEqual(MapPoint a, MapPoint b) =>
        Math.Abs(a.X - b.X) < PointEpsilon && Math.Abs(a.Y - b.Y) < PointEpsilon;
}
