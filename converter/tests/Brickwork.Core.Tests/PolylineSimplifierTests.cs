using Brickwork.Core.Geometry;
using Brickwork.Core.Models;
using Brickwork.Inkarnate;
using Brickwork.Inkarnate.Parsing;
using Xunit;

namespace Brickwork.Core.Tests;

public class PolylineSimplifierTests
{
    [Fact]
    public void DouglasPeucker_KeepsEndpoints_ForStraightLine()
    {
        var points = new[]
        {
            new MapPoint(0, 0),
            new MapPoint(100, 0),
        };

        var simplified = PolylineSimplifier.DouglasPeucker(points, tolerance: 25);

        Assert.Equal(2, simplified.Count);
    }

    [Fact]
    public void DouglasPeucker_RemovesCollinearMiddlePoint()
    {
        var points = new[]
        {
            new MapPoint(0, 0),
            new MapPoint(50, 0),
            new MapPoint(100, 0),
        };

        var simplified = PolylineSimplifier.DouglasPeucker(points, tolerance: 1);

        Assert.Equal(2, simplified.Count);
        Assert.Equal(0, simplified[0].X, precision: 6);
        Assert.Equal(100, simplified[1].X, precision: 6);
    }

    [Fact]
    public void DouglasPeucker_KeepsCornerPoint()
    {
        var points = new[]
        {
            new MapPoint(0, 0),
            new MapPoint(50, 50),
            new MapPoint(100, 0),
        };

        var simplified = PolylineSimplifier.DouglasPeucker(points, tolerance: 5);

        Assert.Equal(3, simplified.Count);
    }

    [Fact]
    public void DouglasPeucker_ReturnsCopy_WhenToleranceZero()
    {
        var points = new[]
        {
            new MapPoint(0, 0),
            new MapPoint(50, 50),
            new MapPoint(100, 0),
        };

        var simplified = PolylineSimplifier.DouglasPeucker(points, tolerance: 0);

        Assert.Equal(3, simplified.Count);
    }
}

public class WallPointSimplifierTests
{
    [Fact]
    public async Task ImportAsync_SimplifiesCurvedWalls_WithDefaultTolerance()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "test-maps", "basic-walls.ink"));
        await using var input = File.OpenRead(path);
        var map = await new InkarnateImporter().ImportAsync(input);

        var straightWall = map.Walls.Single(wall => wall.EntityId == 3);
        var bezierWall = map.Walls.Single(wall => wall.EntityId == 4);
        var freehandWall = map.Walls.Single(wall => wall.EntityId == 2);

        Assert.Equal(2, straightWall.Points.Count);

        // Default tolerance is 20 scene units (more detail than the old 50).
        Assert.InRange(bezierWall.Points.Count, 2, 20);
        Assert.InRange(freehandWall.Points.Count, 4, 40);
    }

    [Fact]
    public void Apply_ReducesSampledBezierPoints()
    {
        var sampled = InkSvgPathParser.ParseToScenePoints(
            "M0,0c50,50 100,0 150,50",
            originX: 0,
            originY: 0,
            scale: 1);

        var wall = new Wall
        {
            EntityId = 1,
            Points = sampled.ToList(),
        };
        var beforeCount = wall.Points.Count;

        WallPointSimplifier.Apply(wall, tolerance: 25);

        Assert.True(wall.Points.Count < beforeCount);
        Assert.True(wall.Points.Count >= 2);
    }
}
