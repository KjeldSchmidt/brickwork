using Brickwork.Core.Models;
using Brickwork.Inkarnate;
using Xunit;

namespace Brickwork.Core.Tests;

public class BasicRegionsImportTests
{
    private static string BasicRegionsInkPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "test-maps", "basic-regions.ink"));

    [Fact]
    public async Task ImportAsync_DetectsColorNamedAndLayerRegions()
    {
        Assert.True(File.Exists(BasicRegionsInkPath), $"Missing test resource: {BasicRegionsInkPath}");

        var importer = new InkarnateImporter();
        await using var input = File.OpenRead(BasicRegionsInkPath);
        var map = await importer.ImportAsync(input);

        var regionById = map.Regions.ToDictionary(region => region.EntityId);
        Assert.True(regionById.ContainsKey(6), "Entity 6 (Difficult Terrain layer) should be a region.");
        Assert.True(regionById.ContainsKey(7), "Entity 7 (color path) should be a region.");
        Assert.True(regionById.ContainsKey(9), "Entity 9 (greater difficult name) should be a region.");

        Assert.Equal(RegionType.DifficultTerrain, regionById[6].RegionType);
        Assert.Equal(RegionType.DifficultTerrain, regionById[7].RegionType);
        Assert.Equal(RegionType.GreaterDifficultTerrain, regionById[9].RegionType);

        Assert.DoesNotContain(map.Walls, wall => wall.EntityId is 6 or 7 or 9);
        Assert.Contains(map.Walls, wall => wall.EntityId == 5);
        Assert.All(map.Regions, region => Assert.True(region.Points.Count >= 3));
    }

    [Fact]
    public async Task ImportAsync_ColorRuleOff_KeepsColorPathAsWall()
    {
        Assert.True(File.Exists(BasicRegionsInkPath), $"Missing test resource: {BasicRegionsInkPath}");

        var importer = new InkarnateImporter
        {
            Options = new InkarnateImportOptions
            {
                DetectRegionsFromColorPaths = false,
                DetectRegionsFromNamedContainers = true,
                DetectRegionsFromPathNames = true,
            },
        };
        await using var input = File.OpenRead(BasicRegionsInkPath);
        var map = await importer.ImportAsync(input);

        Assert.Contains(map.Walls, wall => wall.EntityId == 7);
        Assert.DoesNotContain(map.Regions, region => region.EntityId == 7);
        Assert.Contains(map.Regions, region => region.EntityId == 6);
        Assert.Contains(map.Regions, region => region.EntityId == 9);
    }

    [Fact]
    public async Task ImportAsync_ContainerRuleOff_KeepsLayerPathAsWall()
    {
        Assert.True(File.Exists(BasicRegionsInkPath), $"Missing test resource: {BasicRegionsInkPath}");

        var importer = new InkarnateImporter
        {
            Options = new InkarnateImportOptions
            {
                DetectRegionsFromColorPaths = true,
                DetectRegionsFromNamedContainers = false,
                DetectRegionsFromPathNames = true,
            },
        };
        await using var input = File.OpenRead(BasicRegionsInkPath);
        var map = await importer.ImportAsync(input);

        Assert.Contains(map.Walls, wall => wall.EntityId == 6);
        Assert.DoesNotContain(map.Regions, region => region.EntityId == 6);
        Assert.Contains(map.Regions, region => region.EntityId == 7);
        Assert.Contains(map.Regions, region => region.EntityId == 9);
    }

    [Fact]
    public async Task ImportAsync_PathNameRuleOff_KeepsNamedPathAsWallUnlessOtherRuleMatches()
    {
        Assert.True(File.Exists(BasicRegionsInkPath), $"Missing test resource: {BasicRegionsInkPath}");

        var importer = new InkarnateImporter
        {
            Options = new InkarnateImportOptions
            {
                DetectRegionsFromColorPaths = false,
                DetectRegionsFromNamedContainers = false,
                DetectRegionsFromPathNames = false,
            },
        };
        await using var input = File.OpenRead(BasicRegionsInkPath);
        var map = await importer.ImportAsync(input);

        Assert.Empty(map.Regions);
        Assert.Contains(map.Walls, wall => wall.EntityId == 5);
        Assert.Contains(map.Walls, wall => wall.EntityId == 6);
        Assert.Contains(map.Walls, wall => wall.EntityId == 7);
        Assert.Contains(map.Walls, wall => wall.EntityId == 9);
    }
}
