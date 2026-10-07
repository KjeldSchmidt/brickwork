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

    [Fact]
    public void RestoreTo_ReappliesLayerRenameInPlace()
    {
        var map = new MapDocument();
        var layer = MapLayerEditing.EnsureDefaultLayer(map);

        var before = DocumentContentMemento.Capture(map);
        layer.Name = "Renamed";
        before.RestoreTo(map);

        Assert.Same(layer, map.Layers.Single());
        Assert.Equal(MapLayerEditing.DefaultLayerName, layer.Name);
    }

    [Fact]
    public void RestoreTo_ReappliesRegionMutationInPlace()
    {
        var map = new MapDocument();
        var region = new Region
        {
            EntityId = 1,
            RegionType = RegionType.Other,
            Points = { new MapPoint(0, 0), new MapPoint(10, 0), new MapPoint(10, 10) },
        };
        map.Regions.Add(region);

        var before = DocumentContentMemento.Capture(map);
        region.RegionType = RegionType.DifficultTerrain;
        region.Points[1] = new MapPoint(20, 0);
        var after = DocumentContentMemento.Capture(map);

        Assert.False(before.ContentEquals(after));

        before.RestoreTo(map);

        Assert.Same(region, map.Regions.Single());
        Assert.Equal(RegionType.Other, region.RegionType);
        Assert.Equal(new MapPoint(10, 0), region.Points[1]);
    }
}
