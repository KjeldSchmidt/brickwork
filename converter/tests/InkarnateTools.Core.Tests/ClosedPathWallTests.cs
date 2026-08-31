using InkarnateTools.Core.Geometry;
using InkarnateTools.Core.Models;
using InkarnateTools.Inkarnate;
using Xunit;

namespace InkarnateTools.Core.Tests;

public class WallPolylineEdgesTests
{
    [Fact]
    public void EnumerateEdges_ClosedSquare_HasFourEdges()
    {
        var square = new List<MapPoint>
        {
            new(0, 0),
            new(10, 0),
            new(10, 10),
            new(0, 10),
        };

        var edges = WallPolylineEdges.EnumerateEdges(square, isClosed: true).ToList();

        Assert.Equal(4, edges.Count);
        Assert.Equal(40, WallPolylineEdges.TotalLength(square, isClosed: true), precision: 6);
    }

    [Fact]
    public void EnumerateEdges_OpenLine_HasOneEdge()
    {
        var line = new List<MapPoint> { new(0, 0), new(10, 0) };
        var edges = WallPolylineEdges.EnumerateEdges(line, isClosed: false).ToList();

        Assert.Single(edges);
    }

    [Fact]
    public async Task ImportAsync_ClosedPathWall_HasFourEdgesAndFourPoints()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "basic-walls.ink"));
        await using var input = File.OpenRead(path);
        var map = await new InkarnateImporter().ImportAsync(input);

        var closedWall = map.Walls.Single(wall => wall.EntityId == 6);

        Assert.Equal(4, closedWall.Points.Count);
        Assert.True(closedWall.IsClosed);
        Assert.Equal(4, WallPolylineEdges.EdgeCount(closedWall.Points, closedWall.IsClosed));
    }
}

public class ClosedTerrainRingTests
{
    [Fact]
    public async Task BuildClosedRing_ImportedSquare_InnerFitsInsideOuter()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "basic-walls.ink"));
        await using var input = File.OpenRead(path);
        var map = await new InkarnateImporter().ImportAsync(input);

        var closedWall = map.Walls.Single(wall => wall.EntityId == 6);
        var ring = WallThicknessPolygonBuilder.BuildClosedRing(closedWall.Points, closedWall.SceneThickness);

        Assert.NotNull(ring);
        Assert.Equal(4, ring!.Outer.Count);
        Assert.Equal(4, ring.Inner.Count);

        var outerBounds = Bounds(ring.Outer);
        var innerBounds = Bounds(ring.Inner);

        Assert.True(innerBounds.MinX > outerBounds.MinX);
        Assert.True(innerBounds.MaxX < outerBounds.MaxX);
        Assert.True(innerBounds.MinY > outerBounds.MinY);
        Assert.True(innerBounds.MaxY < outerBounds.MaxY);

        var outerArea = PolygonArea(ring.Outer);
        var innerArea = PolygonArea(ring.Inner);
        Assert.True(outerArea > innerArea);
        Assert.InRange(outerArea - innerArea, 100_000, 600_000);
    }

    [Fact]
    public void DouglasPeucker_ClosedSquare_KeepsFourCorners()
    {
        var square = new List<MapPoint>
        {
            new(0, 0),
            new(100, 0),
            new(100, 100),
            new(0, 100),
        };

        var simplified = PolylineSimplifier.DouglasPeucker(square, tolerance: 50, isClosed: true);

        Assert.Equal(4, simplified.Count);
    }

    private static (double MinX, double MaxX, double MinY, double MaxY) Bounds(IReadOnlyList<MapPoint> points)
    {
        return (
            points.Min(p => p.X),
            points.Max(p => p.X),
            points.Min(p => p.Y),
            points.Max(p => p.Y));
    }

    private static double PolygonArea(IReadOnlyList<MapPoint> points)
    {
        double area = 0;
        for (var i = 0; i < points.Count; i++)
        {
            var j = (i + 1) % points.Count;
            area += points[i].X * points[j].Y;
            area -= points[j].X * points[i].Y;
        }

        return Math.Abs(area) / 2d;
    }
}
