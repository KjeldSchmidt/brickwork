using Brickwork.Core.Editing;
using Brickwork.Core.Models;
using Xunit;

namespace Brickwork.Core.Tests;

public class MapEntityClipboardTests
{
    [Fact]
    public void PasteInto_CreatesOffsetCopiesWithNewIds()
    {
        var map = new MapDocument();
        map.Walls.Add(new Wall
        {
            EntityId = 1,
            Name = "A",
            Points = { new MapPoint(0, 0), new MapPoint(10, 0) },
        });
        map.Regions.Add(new Region
        {
            EntityId = 2,
            Name = "R",
            Points =
            {
                new MapPoint(0, 0),
                new MapPoint(5, 0),
                new MapPoint(5, 5),
            },
        });

        var clipboard = MapEntityClipboard.Capture(map, [1], [2]);
        var (walls, regions) = clipboard.PasteInto(map, offsetX: 40, offsetY: 40);

        Assert.Single(walls);
        Assert.Single(regions);
        Assert.Equal(3, walls[0].EntityId);
        Assert.Equal(4, regions[0].EntityId);
        Assert.Equal(new MapPoint(40, 40), walls[0].Points[0]);
        Assert.Equal(new MapPoint(40, 40), regions[0].Points[0]);
        Assert.Equal(2, map.Walls.Count);
        Assert.Equal(2, map.Regions.Count);
    }

    [Fact]
    public void PasteInto_AssignsFreshPortalIds()
    {
        var map = new MapDocument();
        map.Walls.Add(new Wall
        {
            EntityId = 1,
            Points = { new MapPoint(0, 0), new MapPoint(20, 0) },
            Portals =
            {
                new WallPortal { Id = "portal-a", Anchor = new MapPoint(10, 0), Width = 2 },
            },
        });

        var clipboard = MapEntityClipboard.Capture(map, [1], []);
        var (walls, _) = clipboard.PasteInto(map, 10, 10);

        Assert.NotEqual("portal-a", walls[0].Portals[0].Id);
        Assert.False(string.IsNullOrWhiteSpace(walls[0].Portals[0].Id));
    }

    [Fact]
    public void PasteInto_KeepsGroupMembership()
    {
        var map = new MapDocument();
        map.Groups.Add(new EntityGroup
        {
            GroupId = 7,
            Name = "Bundle",
            MemberIds = { 1 },
        });
        map.Walls.Add(new Wall
        {
            EntityId = 1,
            GroupId = 7,
            Points = { new MapPoint(0, 0), new MapPoint(10, 0) },
        });

        var clipboard = MapEntityClipboard.Capture(map, [1], []);
        var (walls, _) = clipboard.PasteInto(map, 40, 40);

        Assert.Equal(7, walls[0].GroupId);
        Assert.Contains(1, map.Groups[0].MemberIds);
        Assert.Contains(walls[0].EntityId, map.Groups[0].MemberIds);
    }
}
