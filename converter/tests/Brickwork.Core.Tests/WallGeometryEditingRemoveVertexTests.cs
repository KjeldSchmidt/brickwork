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

    [Fact]
    public void TryAddPortal_SnapsDoorOntoCenterline()
    {
        var wall = new Wall
        {
            EntityId = 1,
            Points = { new MapPoint(0, 0), new MapPoint(100, 0) },
        };

        var portal = WallGeometryEditing.TryAddPortal(wall, new MapPoint(40, 8), defaultWidth: 20);

        Assert.NotNull(portal);
        Assert.Single(wall.Portals);
        Assert.Equal(WallLineType.Door, portal!.LineType);
        Assert.Equal(20, portal.Width);
        Assert.Equal(new MapPoint(40, 0), MapPointTransforms.LocalToScene(wall, portal.Anchor));
    }
}
