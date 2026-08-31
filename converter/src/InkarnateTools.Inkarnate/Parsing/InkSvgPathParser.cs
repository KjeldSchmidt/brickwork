using InkarnateTools.Core.Models;
using SkiaSharp;

namespace InkarnateTools.Inkarnate.Parsing;

internal static class InkSvgPathParser
{
    private const float TessellationTolerance = 1f;

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

        using var measure = new SKPathMeasure(path, false);
        var length = measure.Length;
        if (length <= 0)
        {
            return [];
        }

        var points = new List<MapPoint>();
        var distance = 0f;

        while (distance <= length)
        {
            if (measure.GetPositionAndTangent(distance, out var position, out _))
            {
                points.Add(TransformPoint(position, originX, originY, scale));
            }

            distance += TessellationTolerance;
        }

        if (measure.GetPositionAndTangent(length, out var endPosition, out _))
        {
            var endPoint = TransformPoint(endPosition, originX, originY, scale);
            if (points.Count == 0 || !NearlyEqual(points[^1], endPoint))
            {
                points.Add(endPoint);
            }
        }

        return points;
    }

    private static MapPoint TransformPoint(SKPoint local, double originX, double originY, double scale) =>
        new(local.X * scale + originX, local.Y * scale + originY);

    private static bool NearlyEqual(MapPoint a, MapPoint b) =>
        Math.Abs(a.X - b.X) < 0.01 && Math.Abs(a.Y - b.Y) < 0.01;
}
