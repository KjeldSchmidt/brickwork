namespace InkarnateTools.Core.Geometry;

public static class WallCircularIntervals
{
    private const double Epsilon = 1e-6;

    public static double NormalizeArcLength(double length, double totalLength)
    {
        if (totalLength <= Epsilon)
        {
            return 0d;
        }

        var normalized = length % totalLength;
        if (normalized < 0)
        {
            normalized += totalLength;
        }

        if (normalized >= totalLength - Epsilon)
        {
            return 0d;
        }

        return normalized;
    }

    public static List<(double Start, double End)> ExpandPortalGap(
        double center,
        double halfWidth,
        double totalLength,
        bool isClosed)
    {
        var rawStart = center - halfWidth;
        var rawEnd = center + halfWidth;

        if (!isClosed)
        {
            var start = Math.Max(0d, rawStart);
            var end = Math.Min(totalLength, rawEnd);
            return end - start > Epsilon ? [(start, end)] : [];
        }

        if (totalLength <= Epsilon)
        {
            return [];
        }

        if (rawEnd - rawStart >= totalLength - Epsilon)
        {
            return [(0d, totalLength)];
        }

        var gaps = new List<(double Start, double End)>();
        if (rawStart < -Epsilon)
        {
            gaps.Add((totalLength + rawStart, totalLength));
            rawStart = 0d;
        }

        if (rawEnd > totalLength + Epsilon)
        {
            gaps.Add((0d, rawEnd - totalLength));
            rawEnd = totalLength;
        }

        if (rawEnd - rawStart > Epsilon)
        {
            gaps.Add((rawStart, rawEnd));
        }

        return gaps;
    }

    public static (double Start, double End) GetUnclampedPortalInterval(
        double center,
        double halfWidth,
        double totalLength,
        bool isClosed)
    {
        var start = center - halfWidth;
        var end = center + halfWidth;

        if (!isClosed)
        {
            return (Math.Max(0d, start), Math.Min(totalLength, end));
        }

        if (totalLength <= Epsilon || end - start >= totalLength - Epsilon)
        {
            return (0d, totalLength);
        }

        return (start, end);
    }

    public static bool IntervalWraps(double start, double end, double totalLength, bool isClosed) =>
        isClosed &&
        totalLength > Epsilon &&
        (start < -Epsilon || end > totalLength + Epsilon);

    public static double ForwardArcDistance(double from, double to, double totalLength)
    {
        if (totalLength <= Epsilon)
        {
            return 0d;
        }

        var normalizedFrom = NormalizeArcLength(from, totalLength);
        var normalizedTo = NormalizeArcLength(to, totalLength);
        var distance = normalizedTo - normalizedFrom;
        if (distance < -Epsilon)
        {
            distance += totalLength;
        }

        return Math.Max(0d, distance);
    }

    public static double MaxPortalHalfWidth(double center, double totalLength, bool isClosed)
    {
        if (!isClosed)
        {
            return Math.Max(0d, Math.Min(center, totalLength - center));
        }

        return totalLength > Epsilon ? totalLength / 2d - Epsilon : 0d;
    }
}
