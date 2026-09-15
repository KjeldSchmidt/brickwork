using Brickwork.Core.Editing;
using Brickwork.Core.Models;
using Xunit;

namespace Brickwork.Core.Tests;

public class DocumentContentMementoTests
{
    [Fact]
    public void RestoreTo_ReappliesWallMutationInPlace()
    {
        var map = new MapDocument();
        var wall = new Wall
        {
            EntityId = 1,
            LineType = WallLineType.Solid,
            Points = { new MapPoint(0, 0), new MapPoint(10, 0) },
        };
        map.Walls.Add(wall);

        var before = DocumentContentMemento.Capture(map);
        wall.LineType = WallLineType.Door;
        wall.Points[1] = new MapPoint(20, 0);
        var after = DocumentContentMemento.Capture(map);

        Assert.False(before.ContentEquals(after));

        before.RestoreTo(map);

        Assert.Same(wall, map.Walls.Single());
        Assert.Equal(WallLineType.Solid, wall.LineType);
        Assert.Equal(new MapPoint(10, 0), wall.Points[1]);
    }

    [Fact]
    public void RestoreTo_ReinsertsRemovedWall()
    {
        var map = new MapDocument();
        map.Walls.Add(new Wall { EntityId = 1, Points = { new MapPoint(0, 0), new MapPoint(1, 0) } });
        map.Walls.Add(new Wall { EntityId = 2, Points = { new MapPoint(0, 1), new MapPoint(1, 1) } });

        var before = DocumentContentMemento.Capture(map);
        map.Walls.RemoveAt(1);
        before.RestoreTo(map);

        Assert.Equal(new[] { 1, 2 }, map.Walls.Select(wall => wall.EntityId));
    }
}
