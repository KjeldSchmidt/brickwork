using System.Text.Json;
using Brickwork.Composition;
using Brickwork.Core.Editing;
using Brickwork.Core.Models;
using Xunit;

namespace Brickwork.Core.Tests;

public class SceneLevelTests
{
    [Fact]
    public void Set_StoresAllLevelsAsEmpty_AndRefusesNoLevel()
    {
        var levels = new HashSet<int>();

        Assert.True(SceneLevels.Set(levels, 2, on: false, levelCount: 3));
        Assert.Equal([1, 3], levels.Order());
        Assert.False(SceneLevels.IsOn(levels, 2));

        Assert.True(SceneLevels.Set(levels, 1, on: false, levelCount: 3));
        Assert.Equal([3], levels.Order());

        // Taking it off its last level would leave it nowhere.
        Assert.False(SceneLevels.Set(levels, 3, on: false, levelCount: 3));
        Assert.Equal([3], levels.Order());

        SceneLevels.Set(levels, 1, on: true, levelCount: 3);
        SceneLevels.Set(levels, 2, on: true, levelCount: 3);
        Assert.Empty(levels);
        Assert.True(SceneLevels.IsOn(levels, 2));
    }

    [Fact]
    public void Effective_IgnoresLevelsBeyondTheCount()
    {
        Assert.Equal([2], SceneLevels.Effective(new HashSet<int> { 2, 5 }, 3));
        Assert.Empty(SceneLevels.Effective(new HashSet<int> { 1, 2 }, 2));
        Assert.Empty(SceneLevels.Effective(new HashSet<int> { 4 }, 3));
        // Only on a level that no longer exists: shown and exported as on every level.
        Assert.True(SceneLevels.IsOn(new HashSet<int> { 4 }, 1, levelCount: 3));
    }

    [Fact]
    public void Memento_SeesAndRestoresLevelChanges()
    {
        var map = new MapDocument();
        var wall = new Wall { EntityId = 1, Points = { new MapPoint(0, 0), new MapPoint(10, 0) } };
        map.Walls.Add(wall);

        var before = DocumentContentMemento.Capture(map);
        wall.Levels.Add(2);
        Assert.False(before.ContentEquals(DocumentContentMemento.Capture(map)));

        before.RestoreTo(map);
        Assert.Empty(wall.Levels);
    }

    [Fact]
    public async Task FoundryExport_WritesLevelsAndEntityLevels()
    {
        var map = new MapDocument
        {
            Scene = new SceneDimensions { Width = 1000, Height = 1000 },
            Preview = new PreviewDimensions { Width = 1000, Height = 1000 },
            SceneLevelCount = 3,
            SceneLevelHeight = 20,
        };
        map.Walls.Add(new Wall
        {
            EntityId = 1,
            Points = { new MapPoint(0, 0), new MapPoint(500, 0) },
            Levels = [2],
        });
        map.Walls.Add(new Wall { EntityId = 2, Points = { new MapPoint(0, 100), new MapPoint(500, 100) } });
        map.Lights.Add(new LightSource { EntityId = 3, Position = new MapPoint(10, 10), Levels = [2, 3] });
        map.Lights.Add(new LightSource { EntityId = 5, Position = new MapPoint(20, 20) });
        map.Regions.Add(new Region
        {
            EntityId = 4,
            Points = { new MapPoint(0, 0), new MapPoint(100, 0), new MapPoint(100, 100) },
            Levels = [3],
        });

        await using var output = new MemoryStream();
        await ServiceFactory.CreateConvertMapService().ConvertAsync(map, output, "foundry");
        output.Position = 0;
        using var document = await JsonDocument.ParseAsync(output);
        var root = document.RootElement;

        // Foundry only keeps "levels" on import when the scene is from a version that has them.
        Assert.Equal("14.353", root.GetProperty("_stats").GetProperty("coreVersion").GetString());

        var levels = root.GetProperty("levels").EnumerateArray().ToList();
        Assert.Equal(3, levels.Count);
        Assert.Equal("defaultLevel0000", levels[0].GetProperty("_id").GetString());
        Assert.Equal("brickworkLevel02", levels[1].GetProperty("_id").GetString());
        Assert.Equal(20, levels[1].GetProperty("elevation").GetProperty("bottom").GetDouble());
        Assert.Equal(40, levels[1].GetProperty("elevation").GetProperty("top").GetDouble());
        // Only level 1 shows the map; level 3 sees the two levels below it.
        Assert.Equal(JsonValueKind.String, levels[0].GetProperty("background").GetProperty("src").ValueKind);
        Assert.Equal(JsonValueKind.Null, levels[2].GetProperty("background").GetProperty("src").ValueKind);
        Assert.Equal(
            ["defaultLevel0000", "brickworkLevel02"],
            levels[2].GetProperty("visibility").GetProperty("levels").EnumerateArray().Select(id => id.GetString()));

        var wallLevels = root.GetProperty("walls").EnumerateArray()
            .Select(wall => wall.GetProperty("levels").EnumerateArray().Select(id => id.GetString()).ToList())
            .ToList();
        Assert.Contains(wallLevels, ids => ids.SequenceEqual(["brickworkLevel02"]));
        Assert.Contains(wallLevels, ids => ids.Count == 0);

        // A light on several levels becomes one light on each, at that level's floor.
        var lights = root.GetProperty("lights").EnumerateArray()
            .Select(light => (
                Levels: light.GetProperty("levels").EnumerateArray().Select(id => id.GetString()).ToList(),
                Elevation: light.GetProperty("elevation").GetDouble()))
            .ToList();
        Assert.Equal(5, lights.Count);
        Assert.Equal(["brickworkLevel02"], lights[0].Levels);
        Assert.Equal(20, lights[0].Elevation);
        Assert.Equal(["brickworkLevel03"], lights[1].Levels);
        Assert.Equal(40, lights[1].Elevation);
        Assert.Equal([0d, 20d, 40d], lights.Skip(2).Select(light => light.Elevation));
        Assert.All(lights.Skip(2), light => Assert.Single(light.Levels));

        var region = root.GetProperty("regions")[0];
        Assert.Equal(["brickworkLevel03"], region.GetProperty("levels").EnumerateArray().Select(id => id.GetString()));
        Assert.Equal(40, region.GetProperty("elevation").GetProperty("bottom").GetDouble());
        Assert.Equal(60, region.GetProperty("elevation").GetProperty("top").GetDouble());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FoundryExport_WritesGlobalIllumination(bool enabled)
    {
        var map = new MapDocument
        {
            Scene = new SceneDimensions { Width = 1000, Height = 1000 },
            Preview = new PreviewDimensions { Width = 1000, Height = 1000 },
            GlobalIllumination = enabled,
        };

        await using var output = new MemoryStream();
        await ServiceFactory.CreateConvertMapService().ConvertAsync(map, output, "foundry");
        output.Position = 0;
        using var document = await JsonDocument.ParseAsync(output);

        Assert.Equal(
            enabled,
            document.RootElement.GetProperty("environment").GetProperty("globalLight").GetProperty("enabled").GetBoolean());
    }
}
