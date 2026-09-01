using InkarnateTools.Core.Geometry;
using InkarnateTools.Core.Models;
using Xunit;

namespace InkarnateTools.Core.Tests;

public class WallGeometryEditingTests
{
    [Fact]
    public void SceneToLocal_InvertsLocalToScene()
    {
        var wall = new Wall
        {
            EntityId = 1,
            PathOrigin = new MapPoint(100, 200),
            RotationPivot = new MapPoint(100, 200),
            Angle = 30,
            Scale = 1.5,
        };

        var local = new MapPoint(40, -10);
        var scene = MapPointTransforms.LocalToScene(wall, local);
        var roundTrip = MapPointTransforms.SceneToLocal(wall, scene);

        Assert.Equal(local.X, roundTrip.X, precision: 3);
        Assert.Equal(local.Y, roundTrip.Y, precision: 3);
    }

    [Fact]
    public void SetVertexPosition_UpdatesWallPoint()
    {
        var wall = new Wall
        {
            EntityId = 1,
            Points = [new MapPoint(0, 0), new MapPoint(100, 0)],
        };

        WallGeometryEditing.SetVertexPosition(wall, 1, new MapPoint(120, 5));

        Assert.Equal(120, wall.Points[1].X, precision: 3);
        Assert.Equal(5, wall.Points[1].Y, precision: 3);
    }

    [Fact]
    public void SetPortalAnchorFromScene_SnapsToCenterline()
    {
        var wall = new Wall
        {
            EntityId = 1,
            Points = [new MapPoint(0, 0), new MapPoint(100, 0)],
            Portals = [new WallPortal { Width = 20 }],
        };

        WallGeometryEditing.SetPortalAnchorFromScene(wall, wall.Portals[0], new MapPoint(50, 40));

        var anchorScene = WallPathSegmentBuilder.PortalAnchorToScene(wall, wall.Portals[0]);
        Assert.Equal(50, anchorScene.X, precision: 1);
        Assert.Equal(0, anchorScene.Y, precision: 1);
    }

    [Fact]
    public void SetPortalEndpointFromScene_ExpandsWidthFromStartHandle()
    {
        var wall = new Wall
        {
            EntityId = 1,
            Points = [new MapPoint(0, 0), new MapPoint(100, 0)],
            Portals = [new WallPortal { Anchor = new MapPoint(50, 0), Width = 20 }],
        };

        WallGeometryEditing.SetPortalEndpointFromScene(
            wall,
            wall.Portals[0],
            PortalWidthEndpoint.Start,
            new MapPoint(30, 0));

        Assert.Equal(40, wall.Portals[0].Width, precision: 1);
        var anchorScene = WallPathSegmentBuilder.PortalAnchorToScene(wall, wall.Portals[0]);
        Assert.Equal(50, anchorScene.X, precision: 1);
    }

    [Fact]
    public void SetPortalEndpointFromScene_ShrinksWidthSymmetricallyFromEndHandle()
    {
        var wall = new Wall
        {
            EntityId = 1,
            Points = [new MapPoint(0, 0), new MapPoint(100, 0)],
            Portals = [new WallPortal { Anchor = new MapPoint(50, 0), Width = 40 }],
        };

        WallGeometryEditing.SetPortalEndpointFromScene(
            wall,
            wall.Portals[0],
            PortalWidthEndpoint.End,
            new MapPoint(60, 0));

        Assert.Equal(20, wall.Portals[0].Width, precision: 1);
        var anchorScene = WallPathSegmentBuilder.PortalAnchorToScene(wall, wall.Portals[0]);
        Assert.Equal(50, anchorScene.X, precision: 1);
    }

    [Fact]
    public void SetPortalEndpointFromScene_ClosedPathNearSeam_ExpandsWidthAcrossSeam()
    {
        var wall = new Wall
        {
            EntityId = 1,
            IsClosed = true,
            Points =
            [
                new MapPoint(0, 0),
                new MapPoint(100, 0),
                new MapPoint(100, 100),
                new MapPoint(0, 100),
            ],
            Portals = [new WallPortal { Anchor = new MapPoint(0, 0), Width = 20 }],
        };

        WallGeometryEditing.SetPortalEndpointFromScene(
            wall,
            wall.Portals[0],
            PortalWidthEndpoint.Start,
            new MapPoint(0, 15));

        Assert.Equal(30, wall.Portals[0].Width, precision: 1);
        var anchorScene = WallPathSegmentBuilder.PortalAnchorToScene(wall, wall.Portals[0]);
        Assert.Equal(0, anchorScene.X, precision: 1);
        Assert.Equal(0, anchorScene.Y, precision: 1);
    }

    [Fact]
    public void SetPortalEndpointFromScene_ClosedPathNearVertex_DoesNotSnapToMinimumWidth()
    {
        var wall = new Wall
        {
            EntityId = 1,
            IsClosed = true,
            Points =
            [
                new MapPoint(0, 0),
                new MapPoint(100, 0),
                new MapPoint(100, 100),
                new MapPoint(0, 100),
            ],
            Portals = [new WallPortal { Anchor = new MapPoint(5, 0), Width = 20 }],
        };

        WallGeometryEditing.SetPortalEndpointFromScene(
            wall,
            wall.Portals[0],
            PortalWidthEndpoint.End,
            new MapPoint(30, 0));

        Assert.Equal(50, wall.Portals[0].Width, precision: 1);
    }
}
