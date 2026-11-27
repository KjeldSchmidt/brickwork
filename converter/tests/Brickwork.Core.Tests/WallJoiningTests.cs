using Brickwork.Core.Geometry;
using Brickwork.Core.Models;
using Xunit;

namespace Brickwork.Core.Tests;

public class WallJoiningTests
{
    [Fact]
    public void Join_ChainsCollinearSegmentsAndClosesLoop()
    {
        var walls = new[]
        {
            Segment(1, 0, 0, 100, 0),
            Segment(2, 100, 0, 100, 100),
            Segment(3, 100, 100, 0, 100),
            Segment(4, 0, 100, 0, 0),
        };

        var joined = WallJoining.Join(walls);

        var wall = Assert.Single(joined);
        Assert.True(wall.IsClosed);
        Assert.Equal(4, wall.Points.Count);
        Assert.Equal(WallLineType.Solid, wall.LineType);
    }

    [Fact]
    public void Join_PromotesDoorSegmentToPortalOnSolidWall()
    {
        var walls = new[]
        {
            Segment(1, 0, 0, 40, 0),
            Segment(2, 40, 0, 60, 0, WallLineType.Door),
            Segment(3, 60, 0, 100, 0),
        };

        var joined = WallJoining.Join(walls);

        var wall = Assert.Single(joined);
        Assert.False(wall.IsClosed);
        Assert.Equal(WallLineType.Solid, wall.LineType);
        Assert.True(wall.Points.Count >= 3);
        Assert.Equal(new MapPoint(0, 0), wall.Points[0]);
        Assert.Equal(new MapPoint(100, 0), wall.Points[^1]);
        var portal = Assert.Single(wall.Portals);
        Assert.Equal(WallLineType.Door, portal.LineType);
        Assert.Equal(20, portal.Width, precision: 6);
    }

    [Fact]
    public void Join_DoesNotMixLineTypes()
    {
        var walls = new[]
        {
            Segment(1, 0, 0, 50, 0, WallLineType.Solid),
            Segment(2, 50, 0, 100, 0, WallLineType.Terrain),
        };

        var joined = WallJoining.Join(walls);

        Assert.Equal(2, joined.Count);
        Assert.Contains(joined, wall => wall.LineType == WallLineType.Solid);
        Assert.Contains(joined, wall => wall.LineType == WallLineType.Terrain);
    }

    [Fact]
    public void Join_KeepsFreestandingDoorWhenNoHostWall()
    {
        var walls = new[]
        {
            Segment(1, 0, 0, 20, 0, WallLineType.Door),
        };

        var joined = WallJoining.Join(walls);

        var wall = Assert.Single(joined);
        Assert.Equal(WallLineType.Door, wall.LineType);
        Assert.Empty(wall.Portals);
    }

    [Fact]
    public void Join_UsesEndpointTolerance()
    {
        var walls = new[]
        {
            Segment(1, 0, 0, 100, 0),
            Segment(2, 100.5, 0, 200, 0),
        };

        var joined = WallJoining.Join(walls, new WallJoinOptions(EndpointTolerance: 1.5));

        var wall = Assert.Single(joined);
        Assert.True(wall.Points.Count >= 3);
    }

    [Fact]
    public void FoundrySceneMapper_JoinsBasicWallFixture()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "resources",
            "test-maps",
            "fvtt-scene-basic-walls.json"));
        Assert.True(File.Exists(path));

        var map = Brickwork.Exporters.Foundry.FoundrySceneMapper.MapFromJson(File.ReadAllText(path));

        // Fixture walls are separate parallel segments (not endpoint-connected), so still 7.
        Assert.Equal(7, map.Walls.Count);
        Assert.Equal(7, map.Walls.Select(wall => wall.LineType).Distinct().Count());
    }

    private static Wall Segment(
        int id,
        double x0,
        double y0,
        double x1,
        double y1,
        WallLineType lineType = WallLineType.Solid) =>
        new()
        {
            EntityId = id,
            Name = $"Wall {id}",
            LayerId = MapLayerEditing.DefaultLayerId,
            LineType = lineType,
            Origin = new MapPoint(x0, y0),
            PathOrigin = new MapPoint(x0, y0),
            RotationPivot = new MapPoint(x0, y0),
            Points =
            [
                new MapPoint(x0, y0),
                new MapPoint(x1, y1),
            ],
        };
}
