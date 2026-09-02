using InkarnateTools.Core.Geometry;
using InkarnateTools.Core.Models;
using Xunit;

namespace InkarnateTools.Core.Tests;

public class WallThicknessPolygonBuilderTests
{
    [Fact]
    public void BuildOutline_StraightSegment_FormsRectangle()
    {
        var centerline =
            new List<MapPoint>
            {
                new(0, 0),
                new(100, 0),
            };

        var polygon = WallThicknessPolygonBuilder.BuildOutline(centerline, thickness: 20);

        Assert.True(polygon.Count >= 4);
        Assert.Contains(polygon, point => point.Y > 9 && point.X is >= 0 and <= 100);
        Assert.Contains(polygon, point => point.Y < -9 && point.X is >= 0 and <= 100);
    }

    [Fact]
    public void BuildOutline_ReturnsEmpty_WhenThicknessMissing()
    {
        var centerline = new List<MapPoint> { new(0, 0), new(10, 0) };

        Assert.Empty(WallThicknessPolygonBuilder.BuildOutline(centerline, thickness: 0));
    }

    [Fact]
    public void BuildTerrainExportLoops_ClosedSquare_ReturnsOuterAndInnerLoops()
    {
        var square = new List<MapPoint>
        {
            new(0, 0),
            new(100, 0),
            new(100, 100),
            new(0, 100),
        };

        var loops = WallThicknessPolygonBuilder.BuildTerrainExportLoops(square, thickness: 20, isClosed: true);

        Assert.Equal(2, loops.Count);
        Assert.Equal(8, loops[0].Count);
        Assert.Equal(4, loops[1].Count);
    }

    [Fact]
    public void BuildOutline_RightAngle_OuterBevelsAndInnerIntersection()
    {
        var centerline = new List<MapPoint>
        {
            new(0, 0),
            new(100, 0),
            new(100, 100),
        };

        var polygon = WallThicknessPolygonBuilder.BuildOutline(centerline, thickness: 20);

        Assert.Equal(7, polygon.Count);
    }

    [Fact]
    public void BuildClosedRing_Square_InnerLoopUsesIntersectionJoins()
    {
        var square = new List<MapPoint>
        {
            new(0, 0),
            new(100, 0),
            new(100, 100),
            new(0, 100),
        };

        var ring = WallThicknessPolygonBuilder.BuildClosedRing(square, thickness: 20);

        Assert.NotNull(ring);
        Assert.Equal(8, ring!.Outer.Count);
        Assert.Equal(4, ring.Inner.Count);
    }

    [Fact]
    public void BuildTerrainExportLoops_OpenSegment_ReturnsOutlinePolygon()
    {
        var centerline = new List<MapPoint> { new(0, 0), new(100, 0) };

        var loops = WallThicknessPolygonBuilder.BuildTerrainExportLoops(centerline, thickness: 20, isClosed: false);

        var outline = Assert.Single(loops);
        Assert.True(outline.Count >= 4);
    }

    [Fact]
    public async Task BuildOutline_ImportedWall_ProducesPolygon()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "basic-walls.ink"));
        await using var input = File.OpenRead(path);
        var map = await new InkarnateTools.Inkarnate.InkarnateImporter().ImportAsync(input);
        var wall = map.Walls.Single(w => w.EntityId == 3);

        var polygon = WallThicknessPolygonBuilder.BuildOutline(wall.Points.ToList(), wall.SceneThickness);

        Assert.True(polygon.Count >= 4);
    }
}
