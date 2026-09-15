using Brickwork.Core.Geometry;
using Brickwork.Core.Models;
using Xunit;

namespace Brickwork.Core.Tests;

public class WallGeometryEditingRemoveVertexTests
{
    [Fact]
    public void TryRemoveVertex_RemovesMiddleVertexOnOpenWall()
    {
        var wall = new Wall
        {
            EntityId = 1,
            Points =
            {
                new MapPoint(0, 0),
                new MapPoint(50, 0),
                new MapPoint(100, 0),
            },
        };

        Assert.True(WallGeometryEditing.TryRemoveVertex(wall, 1));
        Assert.Equal(2, wall.Points.Count);
        Assert.Equal(new MapPoint(0, 0), wall.Points[0]);
        Assert.Equal(new MapPoint(100, 0), wall.Points[1]);
    }

    [Fact]
    public void TryRemoveVertex_RefusesWhenOpenWallWouldDropBelowTwoPoints()
    {
        var wall = new Wall
        {
            EntityId = 1,
            Points = { new MapPoint(0, 0), new MapPoint(100, 0) },
        };

        Assert.False(WallGeometryEditing.TryRemoveVertex(wall, 0));
        Assert.Equal(2, wall.Points.Count);
    }

    [Fact]
    public void TryRemoveVertex_RefusesWhenClosedWallWouldDropBelowThreePoints()
    {
        var wall = new Wall
        {
            EntityId = 1,
            IsClosed = true,
            Points =
            {
                new MapPoint(0, 0),
                new MapPoint(10, 0),
                new MapPoint(0, 10),
            },
        };

        Assert.False(WallGeometryEditing.TryRemoveVertex(wall, 1));
        Assert.Equal(3, wall.Points.Count);
    }

    [Fact]
    public void TryRemovePortal_RemovesPortalFromWall()
    {
        var portal = new WallPortal
        {
            Id = "p1",
            Anchor = new MapPoint(0.5, 0),
            Width = 10,
        };
        var wall = new Wall
        {
            EntityId = 1,
            Points = { new MapPoint(0, 0), new MapPoint(100, 0) },
            Portals = { portal },
        };

        Assert.True(WallGeometryEditing.TryRemovePortal(wall, portal));
        Assert.Empty(wall.Portals);
    }
}
