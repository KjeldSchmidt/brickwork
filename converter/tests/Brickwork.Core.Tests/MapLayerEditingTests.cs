using Brickwork.Core.Models;
using Xunit;

namespace Brickwork.Core.Tests;

public class MapLayerEditingTests
{
    [Fact]
    public void EnsureDefaultLayer_CreatesNamedDefaultLayer()
    {
        var map = new MapDocument();

        var layer = MapLayerEditing.EnsureDefaultLayer(map);

        Assert.Same(layer, Assert.Single(map.Layers));
        Assert.Equal(MapLayerEditing.DefaultLayerId, layer.Id);
        Assert.Equal(MapLayerEditing.DefaultLayerName, layer.Name);
    }

    [Fact]
    public void EnsureDefaultLayer_ReusesExistingDefaultLayer()
    {
        var map = new MapDocument();
        var first = MapLayerEditing.EnsureDefaultLayer(map);
        var second = MapLayerEditing.EnsureDefaultLayer(map);

        Assert.Same(first, second);
        Assert.Single(map.Layers);
    }

    [Fact]
    public void WallDisplayName_DefaultsToWall()
    {
        var wall = new Wall { EntityId = 42 };

        Assert.Equal("Wall", wall.DisplayName);
    }
}
