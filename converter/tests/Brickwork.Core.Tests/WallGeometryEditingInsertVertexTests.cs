using Brickwork.Core.Geometry;
using Brickwork.Core.Models;
using Xunit;

namespace Brickwork.Core.Tests;

public class WallGeometryEditingInsertVertexTests
{
    [Fact]
    public void TryInsertVertex_SplitsOpenSegmentAtProjection()
    {
        var wall = new Wall
        {
            EntityId = 1,
            Points = { new MapPoint(0, 0), new MapPoint(100, 0) },
        };

        var index = WallGeometryEditing.TryInsertVertex(wall, new MapPoint(40, 5));

        Assert.Equal(1, index);
        Assert.Equal(3, wall.Points.Count);
        Assert.Equal(new MapPoint(40, 0), wall.Points[1]);
    }

    [Fact]
    public void TryInsertVertex_ReturnsNullNearExistingVertex()
    {
        var wall = new Wall
        {
            EntityId = 1,
            Points = { new MapPoint(0, 0), new MapPoint(100, 0) },
        };

        Assert.Null(WallGeometryEditing.TryInsertVertex(wall, new MapPoint(0.2, 0)));
        Assert.Equal(2, wall.Points.Count);
    }

    [Fact]
    public void TryInsertVertex_InsertsOnClosedWallClosingEdge()
    {
        var wall = new Wall
        {
            EntityId = 1,
            IsClosed = true,
            Points =
            {
                new MapPoint(0, 0),
                new MapPoint(10, 0),
                new MapPoint(10, 10),
                new MapPoint(0, 10),
            },
        };

        var index = WallGeometryEditing.TryInsertVertex(wall, new MapPoint(0, 5));

        Assert.Equal(4, index);
        Assert.Equal(5, wall.Points.Count);
        Assert.Equal(new MapPoint(0, 5), wall.Points[4]);
    }
}
