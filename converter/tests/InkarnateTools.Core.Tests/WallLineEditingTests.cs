using InkarnateTools.Core.Geometry;
using InkarnateTools.Core.Models;
using Xunit;

namespace InkarnateTools.Core.Tests;

public class WallLineEditingTests
{
    [Fact]
    public void CycleType_AdvancesThroughAllFoundryWallTypes()
    {
        var current = WallLineType.Solid;

        foreach (var expected in new[]
                 {
                     WallLineType.Terrain,
                     WallLineType.Invisible,
                     WallLineType.Ethereal,
                     WallLineType.Door,
                     WallLineType.SecretDoor,
                     WallLineType.Window,
                     WallLineType.Solid,
                 })
        {
            current = WallLineEditing.CycleType(current);
            Assert.Equal(expected, current);
        }
    }

    [Fact]
    public void CycleType_UpdatesPortalWithoutChangingWall()
    {
        var wall = new Wall
        {
            EntityId = 5,
            LineType = WallLineType.Solid,
            Portals = [new WallPortal { Id = "gap-1", LineType = WallLineType.Solid }],
        };

        WallLineEditing.CycleType(wall, wall.Portals[0]);

        Assert.Equal(WallLineType.Solid, wall.LineType);
        Assert.Equal(WallLineType.Terrain, wall.Portals[0].LineType);
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
