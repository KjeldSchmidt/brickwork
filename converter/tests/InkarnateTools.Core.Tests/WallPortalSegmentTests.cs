using InkarnateTools.Core.Geometry;
using InkarnateTools.Core.Models;
using InkarnateTools.Inkarnate;
using Xunit;

namespace InkarnateTools.Core.Tests;

public class WallPortalSegmentTests
{
    private static string BasicWallsInkPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "basic-walls.ink"));

    [Fact]
    public void BuildSegments_WithoutPortals_ReturnsFullPolyline()
    {
        var wall = new Wall
        {
            EntityId = 1,
            Points =
            [
                new MapPoint(0, 0),
                new MapPoint(100, 0),
                new MapPoint(200, 0),
            ],
        };

        var segments = WallPathSegmentBuilder.BuildSegments(wall);

        Assert.Single(segments);
        Assert.Equal(3, segments[0].Count);
    }

    [Fact]
    public void BuildSegments_WithCenterPortal_SplitsIntoTwoSegments()
    {
        var wall = new Wall
        {
            EntityId = 5,
            Origin = new MapPoint(0, 0),
            Scale = 1,
            Points =
            [
                new MapPoint(0, 0),
                new MapPoint(1000, 0),
            ],
            Portals =
            [
                new WallPortal
                {
                    Anchor = new MapPoint(500, 0),
                    Width = 200,
                },
            ],
        };

        var segments = WallPathSegmentBuilder.BuildSegments(wall);

        Assert.Equal(2, segments.Count);
        Assert.Equal(2, segments[0].Count);
        Assert.Equal(2, segments[1].Count);
        Assert.InRange(segments[0][0].X, -1, 1);
        Assert.InRange(segments[0][^1].X, 399, 401);
        Assert.InRange(segments[1][0].X, 599, 601);
        Assert.InRange(segments[1][^1].X, 999, 1001);
    }

    [Fact]
    public async Task BuildSegments_GappedWallFromBasicWallsInk_ProducesGapNearCenter()
    {
        Assert.True(File.Exists(BasicWallsInkPath), $"Missing test resource: {BasicWallsInkPath}");

        await using var input = File.OpenRead(BasicWallsInkPath);
        var map = await new InkarnateImporter().ImportAsync(input);
        var gappedWall = map.Walls.Single(wall => wall.EntityId == 5);

        var segments = WallPathSegmentBuilder.BuildSegments(gappedWall);

        Assert.Equal(2, segments.Count);

        var drawnLength = segments.Sum(segment =>
        {
            var length = 0d;
            for (var i = 1; i < segment.Count; i++)
            {
                var dx = segment[i].X - segment[i - 1].X;
                var dy = segment[i].Y - segment[i - 1].Y;
                length += Math.Sqrt(dx * dx + dy * dy);
            }

            return length;
        });

        var totalLength = 0d;
        for (var i = 1; i < gappedWall.Points.Count; i++)
        {
            var dx = gappedWall.Points[i].X - gappedWall.Points[i - 1].X;
            var dy = gappedWall.Points[i].Y - gappedWall.Points[i - 1].Y;
            totalLength += Math.Sqrt(dx * dx + dy * dy);
        }

        var portal = gappedWall.Portals[0];
        Assert.InRange(drawnLength, totalLength - portal.Width - 5, totalLength - portal.Width + 5);
    }

    [Fact]
    public void PortalAnchorToScene_AppliesWallTransform()
    {
        var wall = new Wall
        {
            Origin = new MapPoint(100, 200),
            PathOrigin = new MapPoint(100, 200),
            Scale = 2,
        };

        var anchor = WallPathSegmentBuilder.PortalAnchorToScene(
            wall,
            new WallPortal { Anchor = new MapPoint(10, 5) });

        Assert.Equal(120, anchor.X, precision: 6);
        Assert.Equal(210, anchor.Y, precision: 6);
    }
}
