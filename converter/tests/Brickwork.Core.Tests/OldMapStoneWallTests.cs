using Brickwork.Core.Geometry;
using Brickwork.Core.Models;
using Brickwork.Inkarnate;
using Brickwork.Inkarnate.Parsing;
using Xunit;

namespace Brickwork.Core.Tests;

public class OldMapStoneWallTests
{
    private const int StoneWallEntityId = 559;

    private static string OldMapInkPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "test-maps", "old-map.ink"));

    [Fact]
    public void IsClosedPath_DetectsSvgCloseVerb()
    {
        const string path = "M-1133.52843,1462.12766l0.00001,-2924.25532l2261.11626,0.00001l5.94059,2924.25531z";
        Assert.True(InkSvgPathParser.IsClosedPath(path));
    }

    [Fact]
    public void IsClosedPath_OpenPolylineWithoutCloseVerb_IsFalse()
    {
        Assert.False(InkSvgPathParser.IsClosedPath("M0,0 L10,0 L10,10"));
    }

    [Fact]
    public async Task ImportAsync_StoneWall_InfersClosedPathFromSvg()
    {
        Assert.True(File.Exists(OldMapInkPath), $"Missing test resource: {OldMapInkPath}");

        await using var input = File.OpenRead(OldMapInkPath);
        var map = await InkarnateFileParser.ParseAsync(input);
        var wall = map.Walls.Single(w => w.EntityId == StoneWallEntityId);

        Assert.True(wall.IsClosed);
        Assert.Equal(4, wall.Points.Count);
        Assert.Equal(7, wall.Portals.Count);
    }

    [Fact]
    public async Task ImportAsync_StoneWall_PlacesBottomPortalsOnBottomEdge()
    {
        Assert.True(File.Exists(OldMapInkPath), $"Missing test resource: {OldMapInkPath}");

        await using var input = File.OpenRead(OldMapInkPath);
        var map = await InkarnateFileParser.ParseAsync(input);
        var wall = map.Walls.Single(w => w.EntityId == StoneWallEntityId);

        var bottomY = wall.Points.Max(point => point.Y);
        var bottomPortals = wall.Portals.Where(portal =>
            Math.Abs(WallPathSegmentBuilder.PortalAnchorToScene(wall, portal).Y - bottomY) < 50).ToList();

        Assert.Equal(3, bottomPortals.Count);

        var segments = WallPathSegmentBuilder.BuildSegments(wall);
        Assert.True(segments.Count >= 4);

        var horizontalBottomSegments = segments.Where(segment =>
            segment.Points.Count >= 2 &&
            segment.Points.All(point => Math.Abs(point.Y - bottomY) < 50)).ToList();
        Assert.NotEmpty(horizontalBottomSegments);
    }
}
