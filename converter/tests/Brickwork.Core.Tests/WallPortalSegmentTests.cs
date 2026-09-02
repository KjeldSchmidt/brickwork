using Brickwork.Core.Geometry;
using Brickwork.Core.Models;
using Brickwork.Inkarnate;
using Xunit;

namespace Brickwork.Core.Tests;

public class WallPortalSegmentTests
{
    private static string BasicWallsInkPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "basic-walls.ink"));

    private static string ClosedGapsInkPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "debug-closed-paths-with-gaps.ink"));

    [Fact]
    public void BuildSegments_WithoutPortals_ReturnsFullPolyline()
    {
        var wall = new Wall
        {
            EntityId = 1,
            IsClosed = true,
            Points =
            [
                new MapPoint(0, 0),
                new MapPoint(100, 0),
                new MapPoint(200, 0),
            ],
        };

        var segments = WallPathSegmentBuilder.BuildSegments(wall);

        var segment = Assert.Single(segments);
        Assert.True(segment.IsClosed);
        Assert.Equal(3, segment.Points.Count);
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
        Assert.All(segments, segment => Assert.False(segment.IsClosed));
        Assert.Equal(2, segments[0].Points.Count);
        Assert.Equal(2, segments[1].Points.Count);
        Assert.InRange(segments[0].Points[0].X, -1, 1);
        Assert.InRange(segments[0].Points[^1].X, 399, 401);
        Assert.InRange(segments[1].Points[0].X, 599, 601);
        Assert.InRange(segments[1].Points[^1].X, 999, 1001);
    }

    [Fact]
    public void BuildSegments_ClosedWithOneGap_ReturnsSingleOpenArc()
    {
        var wall = new Wall
        {
            EntityId = 1,
            IsClosed = true,
            Origin = new MapPoint(0, 0),
            PathOrigin = new MapPoint(0, 0),
            Scale = 1,
            Points =
            [
                new MapPoint(0, 0),
                new MapPoint(100, 0),
                new MapPoint(100, 100),
                new MapPoint(0, 100),
            ],
            Portals =
            [
                new WallPortal
                {
                    Anchor = new MapPoint(50, 0),
                    Width = 20,
                },
            ],
        };

        var segments = WallPathSegmentBuilder.BuildSegments(wall);

        var segment = Assert.Single(segments);
        Assert.False(segment.IsClosed);
        Assert.True(segment.Points.Count >= 4);

        // Gap opens the loop: endpoints should sit on either side of the portal, not coincide.
        Assert.False(
            Math.Abs(segment.Points[0].X - segment.Points[^1].X) <= 1e-3 &&
            Math.Abs(segment.Points[0].Y - segment.Points[^1].Y) <= 1e-3);
        Assert.InRange(segment.Points[0].X, 59, 61);
        Assert.InRange(segment.Points[^1].X, 39, 41);
    }

    [Fact]
    public void BuildSegments_ClosedWithTwoGaps_ReturnsTwoOpenArcs()
    {
        var wall = new Wall
        {
            EntityId = 1,
            IsClosed = true,
            Origin = new MapPoint(0, 0),
            PathOrigin = new MapPoint(0, 0),
            Scale = 1,
            Points =
            [
                new MapPoint(0, 0),
                new MapPoint(100, 0),
                new MapPoint(100, 100),
                new MapPoint(0, 100),
            ],
            Portals =
            [
                new WallPortal { Anchor = new MapPoint(50, 0), Width = 20 },
                new WallPortal { Anchor = new MapPoint(50, 100), Width = 20 },
            ],
        };

        var segments = WallPathSegmentBuilder.BuildSegments(wall);

        Assert.Equal(2, segments.Count);
        Assert.All(segments, segment => Assert.False(segment.IsClosed));
        Assert.All(
            segments,
            segment => Assert.False(
                Math.Abs(segment.Points[0].X - segment.Points[^1].X) <= 1e-3 &&
                Math.Abs(segment.Points[0].Y - segment.Points[^1].Y) <= 1e-3));
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
        Assert.All(segments, segment => Assert.False(segment.IsClosed));

        var drawnLength = segments.Sum(segment =>
        {
            var length = 0d;
            for (var i = 1; i < segment.Points.Count; i++)
            {
                var dx = segment.Points[i].X - segment.Points[i - 1].X;
                var dy = segment.Points[i].Y - segment.Points[i - 1].Y;
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
    public async Task BuildSegments_ClosedGapsInk_DoesNotCloseRemainingArcs()
    {
        Assert.True(File.Exists(ClosedGapsInkPath), $"Missing test resource: {ClosedGapsInkPath}");

        await using var input = File.OpenRead(ClosedGapsInkPath);
        var map = await new InkarnateImporter().ImportAsync(input);

        Assert.NotEmpty(map.Walls);
        foreach (var wall in map.Walls.Where(wall => wall.IsClosed && wall.Portals.Count > 0))
        {
            var segments = WallPathSegmentBuilder.BuildSegments(wall);
            Assert.NotEmpty(segments);
            Assert.All(segments, segment => Assert.False(segment.IsClosed));
            Assert.All(
                segments,
                segment =>
                {
                    var first = segment.Points[0];
                    var last = segment.Points[^1];
                    var dx = first.X - last.X;
                    var dy = first.Y - last.Y;
                    Assert.True(
                        Math.Sqrt(dx * dx + dy * dy) > 1d,
                        $"Closed gapped wall {wall.EntityId} produced a looped segment.");
                });
        }
    }

    [Fact]
    public void BuildSegments_ClosedWithGapCrossingSeam_ReturnsSingleOpenArc()
    {
        var wall = new Wall
        {
            EntityId = 1,
            IsClosed = true,
            Origin = new MapPoint(0, 0),
            PathOrigin = new MapPoint(0, 0),
            Scale = 1,
            Points =
            [
                new MapPoint(0, 0),
                new MapPoint(100, 0),
                new MapPoint(100, 100),
                new MapPoint(0, 100),
            ],
            Portals =
            [
                new WallPortal
                {
                    Anchor = new MapPoint(0, 0),
                    Width = 30,
                },
            ],
        };

        var segments = WallPathSegmentBuilder.BuildSegments(wall);
        var segment = Assert.Single(segments);
        Assert.False(segment.IsClosed);

        var drawnLength = 0d;
        for (var i = 1; i < segment.Points.Count; i++)
        {
            var dx = segment.Points[i].X - segment.Points[i - 1].X;
            var dy = segment.Points[i].Y - segment.Points[i - 1].Y;
            drawnLength += Math.Sqrt(dx * dx + dy * dy);
        }

        Assert.InRange(drawnLength, 370 - 1, 370 + 1);
    }

    [Fact]
    public void BuildPortalSegments_ClosedWithGapCrossingSeam_ReturnsContinuousPolyline()
    {
        var wall = new Wall
        {
            EntityId = 1,
            IsClosed = true,
            Origin = new MapPoint(0, 0),
            PathOrigin = new MapPoint(0, 0),
            Scale = 1,
            Points =
            [
                new MapPoint(0, 0),
                new MapPoint(100, 0),
                new MapPoint(100, 100),
                new MapPoint(0, 100),
            ],
            Portals =
            [
                new WallPortal
                {
                    Anchor = new MapPoint(0, 0),
                    Width = 30,
                },
            ],
        };

        var portalSegment = Assert.Single(WallPathSegmentBuilder.BuildPortalSegments(wall));
        Assert.True(portalSegment.Points.Count >= 3);
        Assert.InRange(portalSegment.Points[0].X, 0, 16);
        Assert.InRange(portalSegment.Points[^1].X, 0, 16);
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
