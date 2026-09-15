using Brickwork.Core.Geometry;
using Brickwork.Core.Models;
using Xunit;

namespace Brickwork.Core.Tests;

public class WallGeometryEditingPortalResnapTests
{
    [Fact]
    public void SetVertexPosition_ResnapsPortalAnchorOntoUpdatedCenterline()
    {
        var wall = new Wall
        {
            EntityId = 1,
            PathOrigin = new MapPoint(0, 0),
            Points =
            {
                new MapPoint(0, 0),
                new MapPoint(100, 0),
            },
        };
        var portal = new WallPortal
        {
            Id = "door",
            Width = 20,
            Anchor = MapPointTransforms.SceneToLocal(wall, new MapPoint(50, 0)),
        };
        wall.Portals.Add(portal);

        WallGeometryEditing.SetVertexPosition(wall, 1, new MapPoint(100, 40));

        var anchorScene = WallPathSegmentBuilder.PortalAnchorToScene(wall, portal);
        var onCenterline = WallGeometryEditing.SnapToCenterline(wall, anchorScene);
        Assert.Equal(onCenterline.X, anchorScene.X, precision: 6);
        Assert.Equal(onCenterline.Y, anchorScene.Y, precision: 6);
        Assert.True(Math.Abs(anchorScene.Y) > 1e-3, "Portal should follow the bent wall off the X axis.");
    }
}
