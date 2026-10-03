using Brickwork.Core.Geometry;
using Brickwork.Core.Models;
using Xunit;

namespace Brickwork.Core.Tests;

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
    public void SetPortalEndpointFromScene_EndHandlePastCenter_ExpandsUsingAbsoluteDistance()
    {
        var wall = new Wall
        {
            EntityId = 1,
            Points = [new MapPoint(0, 0), new MapPoint(100, 0)],
            Portals = [new WallPortal { Anchor = new MapPoint(50, 0), Width = 2 }],
        };

        WallGeometryEditing.SetPortalEndpointFromScene(
            wall,
            wall.Portals[0],
            PortalWidthEndpoint.End,
            new MapPoint(30, 0));

        Assert.Equal(40, wall.Portals[0].Width, precision: 1);
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

    [Fact]
    public void SetTerrainThicknessFromScene_UsesPerpendicularDistanceAtMidpoint()
    {
        var wall = new Wall
        {
            EntityId = 1,
            LineType = WallLineType.Terrain,
            Scale = 1,
            WallThickness = 20,
            Points = [new MapPoint(0, 0), new MapPoint(100, 0)],
        };

        // Midpoint is (50, 0); 15 units off the line → full thickness 30.
        WallGeometryEditing.SetTerrainThicknessFromScene(wall, new MapPoint(50, 15));

        Assert.Equal(30, wall.WallThickness, precision: 1);
    }

    [Fact]
    public void SetTerrainThicknessFromScene_RespectsScale()
    {
        var wall = new Wall
        {
            EntityId = 1,
            LineType = WallLineType.Terrain,
            Scale = 2,
            WallThickness = 20,
            Points = [new MapPoint(0, 0), new MapPoint(100, 0)],
        };

        // Scene half-thickness 20 → entity-local thickness 20.
        WallGeometryEditing.SetTerrainThicknessFromScene(wall, new MapPoint(50, 20));

        Assert.Equal(20, wall.WallThickness, precision: 1);
        Assert.Equal(40, wall.SceneThickness, precision: 1);
    }

    [Fact]
    public void TryGetTerrainThicknessHandleArcLength_UsesPathMidpoint()
    {
        var wall = new Wall
        {
            EntityId = 1,
            LineType = WallLineType.Terrain,
            Points = [new MapPoint(0, 0), new MapPoint(100, 0)],
        };

        Assert.True(WallGeometryEditing.TryGetTerrainThicknessHandleArcLength(wall, out var arcLength));
        Assert.Equal(50, arcLength, precision: 1);
    }

    [Fact]
    public void TryGetTerrainThicknessHandleArcLength_PrefersLongestNonPortalGap()
    {
        var wall = new Wall
        {
            EntityId = 1,
            LineType = WallLineType.Terrain,
            Points = [new MapPoint(0, 0), new MapPoint(100, 0)],
            Portals =
            [
                new WallPortal
                {
                    Id = "gap-1",
                    Anchor = new MapPoint(30, 0),
                    Width = 20,
                },
            ],
        };

        // POIs: 0, 20, 40, 100 → non-portal gaps [0,20] and [40,100]; longest mid = 70.
        Assert.True(WallGeometryEditing.TryGetTerrainThicknessHandleArcLength(wall, out var arcLength));
        Assert.Equal(70, arcLength, precision: 1);
    }

    [Fact]
    public void TryGetTerrainThicknessHandleArcLength_FallsBackToStartWhenFullyPortaled()
    {
        var wall = new Wall
        {
            EntityId = 1,
            LineType = WallLineType.Terrain,
            Points = [new MapPoint(0, 0), new MapPoint(100, 0)],
            Portals =
            [
                new WallPortal
                {
                    Id = "gap-1",
                    Anchor = new MapPoint(50, 0),
                    Width = 100,
                },
            ],
        };

        Assert.True(WallGeometryEditing.TryGetTerrainThicknessHandleArcLength(wall, out var arcLength));
        Assert.Equal(0, arcLength, precision: 1);
    }

    [Fact]
    public void TryGetTerrainThicknessHandleArcLength_ClosedWallWithPortalIgnoresSeam()
    {
        var wall = new Wall
        {
            EntityId = 1,
            LineType = WallLineType.Terrain,
            IsClosed = true,
            Points =
            [
                new MapPoint(0, 0),
                new MapPoint(100, 0),
                new MapPoint(100, 100),
                new MapPoint(0, 100),
            ],
            Portals =
            [
                new WallPortal
                {
                    Id = "gap-1",
                    Anchor = new MapPoint(50, 0),
                    Width = 40,
                },
            ],
        };

        // Perimeter 400; portal [30,70]; seam omitted → one non-portal span [70 → 30], mid = 250.
        Assert.True(WallGeometryEditing.TryGetTerrainThicknessHandleArcLength(wall, out var arcLength));
        Assert.Equal(250, arcLength, precision: 1);
    }

    [Fact]
    public void TryGetTerrainThicknessHandleArcLength_ClosedWallKeepsContinuousGapAcrossSeam()
    {
        var wall = new Wall
        {
            EntityId = 1,
            LineType = WallLineType.Terrain,
            IsClosed = true,
            Points =
            [
                new MapPoint(0, 0),
                new MapPoint(100, 0),
                new MapPoint(100, 100),
                new MapPoint(0, 100),
            ],
            Portals =
            [
                new WallPortal
                {
                    Id = "covers-most",
                    Anchor = new MapPoint(100, 100),
                    Width = 360,
                },
            ],
        };

        // Portal [20,380]; only non-portal span wraps the seam. Omitting the seam keeps one
        // gap of length 40 (mid 0) instead of two length-20 stubs at 10 and 390.
        Assert.True(WallGeometryEditing.TryGetTerrainThicknessHandleArcLength(wall, out var arcLength));
        Assert.Equal(0, arcLength, precision: 1);
    }
}

public class WallVertexHitTesterTests
{
    [Fact]
    public void Pick_SelectsTerrainThicknessEndpointAtThicknessEdge()
    {
        var wall = new Wall
        {
            EntityId = 5,
            LineType = WallLineType.Terrain,
            WallThickness = 40,
            Points = [new MapPoint(0, 0), new MapPoint(1000, 0)],
        };

        var map = new MapDocument
        {
            Scene = new SceneDimensions { Width = 1000, Height = 1000 },
            Preview = new PreviewDimensions { Width = 1000, Height = 1000 },
            Walls = [wall],
        };

        // Midpoint (500,0), half thickness 20 → positive endpoint at (500, 20).
        var hit = WallVertexHitTester.Pick(map, new MapPoint(500, 20), tolerancePreviewPixels: 8);

        Assert.NotNull(hit);
        Assert.Equal(TerrainThicknessEndpoint.Positive, hit!.TerrainThicknessEndpoint);
        Assert.Null(hit.VertexIndex);
        Assert.Null(hit.Portal);
    }

    [Fact]
    public void TryGetEndpoints_SpansFullSceneThickness()
    {
        var wall = new Wall
        {
            EntityId = 1,
            LineType = WallLineType.Terrain,
            WallThickness = 40,
            Scale = 1,
            Points = [new MapPoint(0, 0), new MapPoint(100, 0)],
        };

        Assert.True(TerrainThicknessHandleGeometry.TryGetEndpoints(
            wall,
            out var center,
            out var negative,
            out var positive,
            out _));

        Assert.Equal(50, center.X, precision: 1);
        Assert.Equal(0, center.Y, precision: 1);
        Assert.Equal(50, negative.X, precision: 1);
        Assert.Equal(-20, negative.Y, precision: 1);
        Assert.Equal(50, positive.X, precision: 1);
        Assert.Equal(20, positive.Y, precision: 1);
    }
}
