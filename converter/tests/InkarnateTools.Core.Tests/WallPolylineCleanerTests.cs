using InkarnateTools.Core.Geometry;
using InkarnateTools.Core.Models;
using Xunit;

namespace InkarnateTools.Core.Tests;

public class WallPolylineCleanerTests
{
    [Fact]
    public void DeduplicateClosePoints_RemovesConsecutiveDuplicates()
    {
        var points = new[]
        {
            new MapPoint(0, 0),
            new MapPoint(0.1, 0),
            new MapPoint(10, 0),
        };

        var cleaned = WallPolylineCleaner.DeduplicateClosePoints(points, minDistance: 0.5);

        Assert.Equal(2, cleaned.Count);
        Assert.Equal(0, cleaned[0].X, precision: 6);
        Assert.Equal(10, cleaned[1].X, precision: 6);
    }

    [Fact]
    public void DeduplicateClosePoints_RemovesClosureDuplicate()
    {
        var points = new[]
        {
            new MapPoint(0, 0),
            new MapPoint(10, 0),
            new MapPoint(10, 10),
            new MapPoint(0, 10),
            new MapPoint(0, 0.01),
        };

        var cleaned = WallPolylineCleaner.DeduplicateClosePoints(points, minDistance: 0.5, closeLoop: true);

        Assert.Equal(4, cleaned.Count);
        Assert.Equal(0, cleaned[0].X, precision: 6);
        Assert.Equal(0, cleaned[^1].X, precision: 6);
        Assert.Equal(10, cleaned[^1].Y, precision: 6);
    }
}
