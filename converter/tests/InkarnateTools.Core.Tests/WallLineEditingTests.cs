using InkarnateTools.Core.Geometry;
using InkarnateTools.Core.Models;
using Xunit;

namespace InkarnateTools.Core.Tests;

public class WallLineEditingTests
{
    [Theory]
    [InlineData(WallLineType.Default, WallLineType.Door)]
    [InlineData(WallLineType.Door, WallLineType.Terrain)]
    [InlineData(WallLineType.Terrain, WallLineType.Default)]
    public void CycleType_AdvancesThroughKnownTypes(WallLineType current, WallLineType expected) =>
        Assert.Equal(expected, WallLineEditing.CycleType(current));

    [Fact]
    public void CycleType_UpdatesPortalWithoutChangingWall()
    {
        var wall = new Wall
        {
            EntityId = 5,
            LineType = WallLineType.Default,
            Portals = [new WallPortal { Id = "gap-1", LineType = WallLineType.Default }],
        };

        WallLineEditing.CycleType(wall, wall.Portals[0]);

        Assert.Equal(WallLineType.Default, wall.LineType);
        Assert.Equal(WallLineType.Door, wall.Portals[0].LineType);
    }
}

public class WallHitTesterTests
{
    [Fact]
    public void Pick_SelectsPortalSegmentBeforeWallSegment()
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
                    Id = "gap-1",
                    Anchor = new MapPoint(500, 0),
                    Width = 200,
                },
            ],
        };

        var map = new MapDocument
        {
            Scene = new SceneDimensions { Width = 1000, Height = 1000 },
            Preview = new PreviewDimensions { Width = 1000, Height = 1000 },
            Walls = [wall],
        };

        var hit = WallHitTester.Pick(map, new MapPoint(500, 0), tolerancePreviewPixels: 8);

        Assert.NotNull(hit);
        Assert.Equal("gap-1", hit!.Portal!.Id);
    }
}
