using Brickwork.Core.Geometry;
using Brickwork.Core.Models;
using Xunit;

namespace Brickwork.Core.Tests;

public class EntityTranslateTests
{
    [Fact]
    public void Translate_Wall_MovesPointsAndPathOrigin()
    {
        var wall = new Wall
        {
            EntityId = 1,
            PathOrigin = new MapPoint(10, 20),
            Origin = new MapPoint(10, 20),
            RotationPivot = new MapPoint(15, 25),
            Points =
            {
                new MapPoint(0, 0),
                new MapPoint(100, 0),
            },
            Portals =
            {
                new WallPortal
                {
                    Id = "p1",
                    Anchor = new MapPoint(50, 0),
                    Width = 10,
                },
            },
        };

        WallGeometryEditing.Translate(wall, dx: 5, dy: -3);

        Assert.Equal(new MapPoint(5, -3), wall.Points[0]);
        Assert.Equal(new MapPoint(105, -3), wall.Points[1]);
        Assert.Equal(new MapPoint(15, 17), wall.PathOrigin);
        Assert.Equal(new MapPoint(15, 17), wall.Origin);
        Assert.Equal(new MapPoint(20, 22), wall.RotationPivot);
        // Local portal anchors are unchanged when PathOrigin moves with the wall.
        Assert.Equal(new MapPoint(50, 0), wall.Portals[0].Anchor);
    }

    [Fact]
    public void Translate_Region_MovesPoints()
    {
        var region = new Region
        {
            EntityId = 1,
            Points =
            {
                new MapPoint(0, 0),
                new MapPoint(10, 0),
                new MapPoint(10, 10),
            },
        };

        RegionGeometryEditing.Translate(region, dx: 2, dy: 4);

        Assert.Equal(
            new[] { new MapPoint(2, 4), new MapPoint(12, 4), new MapPoint(12, 14) },
            region.Points.ToArray());
    }
}
