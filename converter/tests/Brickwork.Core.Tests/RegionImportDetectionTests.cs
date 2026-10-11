using Brickwork.Core.Models;
using Brickwork.Inkarnate;
using Xunit;

namespace Brickwork.Core.Tests;

public class RegionImportDetectionTests
{
    [Theory]
    [InlineData("Greater Difficult Terrain", RegionType.GreaterDifficultTerrain)]
    [InlineData("Fence - Greater Difficult Terrain", RegionType.GreaterDifficultTerrain)]
    [InlineData("greater_difficult!!!", RegionType.GreaterDifficultTerrain)]
    [InlineData("Difficult Terrain", RegionType.DifficultTerrain)]
    [InlineData("regions", RegionType.DifficultTerrain)]
    [InlineData("Region", RegionType.DifficultTerrain)]
    [InlineData("REGIONS!!!", RegionType.DifficultTerrain)]
    public void InferRegionTypeFromName_MatchesExpected(string name, RegionType expected)
    {
        Assert.Equal(expected, RegionImportDetection.InferRegionTypeFromName(name));
        Assert.True(RegionImportDetection.MatchesRegionName(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Ellipse")]
    [InlineData("Objects")]
    [InlineData("Wall")]
    public void InferRegionTypeFromName_NonRegionNames_ReturnNull(string? name)
    {
        Assert.Null(RegionImportDetection.InferRegionTypeFromName(name));
        Assert.False(RegionImportDetection.MatchesRegionName(name));
    }

    [Fact]
    public void NormalizeName_StripsNonAlphanumericAndLowercases()
    {
        Assert.Equal("fencedifficultterrain", RegionImportDetection.NormalizeName("Fence - Difficult Terrain!!!"));
    }

    [Fact]
    public void PromoteDetectedRegions_ColorPath_BecomesDifficultTerrain()
    {
        var map = new MapDocument
        {
            Walls =
            [
                new Wall
                {
                    EntityId = 7,
                    Name = "Ellipse",
                    IsClosed = true,
                    Points =
                    [
                        new MapPoint(0, 0),
                        new MapPoint(10, 0),
                        new MapPoint(10, 10),
                    ],
                },
            ],
        };

        var promoted = RegionImportDetection.PromoteDetectedRegions(
            map,
            new Dictionary<int, string> { [7] = "color" },
            new InkarnateImportOptions { DetectRegionsFromColorPaths = true });

        Assert.Equal(1, promoted);
        Assert.Empty(map.Walls);
        var region = Assert.Single(map.Regions);
        Assert.Equal(7, region.EntityId);
        Assert.Equal(RegionType.DifficultTerrain, region.RegionType);
    }

    [Fact]
    public void PromoteDetectedRegions_NamedContainer_UsesLayerType()
    {
        var map = new MapDocument
        {
            Layers = [new MapLayer { Id = "layer-dt", Name = "Difficult Terrain" }],
            Walls =
            [
                new Wall
                {
                    EntityId = 6,
                    Name = "Rectangle",
                    LayerId = "layer-dt",
                    IsClosed = true,
                    Points =
                    [
                        new MapPoint(0, 0),
                        new MapPoint(10, 0),
                        new MapPoint(10, 10),
                    ],
                },
            ],
        };

        var promoted = RegionImportDetection.PromoteDetectedRegions(
            map,
            new Dictionary<int, string> { [6] = "texture" },
            new InkarnateImportOptions
            {
                DetectRegionsFromColorPaths = false,
                DetectRegionsFromNamedContainers = true,
                DetectRegionsFromPathNames = false,
            });

        Assert.Equal(1, promoted);
        Assert.Equal(RegionType.DifficultTerrain, Assert.Single(map.Regions).RegionType);
    }

    [Fact]
    public void PromoteDetectedRegions_DisabledRule_LeavesWall()
    {
        var map = new MapDocument
        {
            Walls =
            [
                new Wall
                {
                    EntityId = 7,
                    Name = "Ellipse",
                    IsClosed = true,
                    Points =
                    [
                        new MapPoint(0, 0),
                        new MapPoint(10, 0),
                        new MapPoint(10, 10),
                    ],
                },
            ],
        };

        var promoted = RegionImportDetection.PromoteDetectedRegions(
            map,
            new Dictionary<int, string> { [7] = "color" },
            new InkarnateImportOptions { DetectRegionsFromColorPaths = false });

        Assert.Equal(0, promoted);
        Assert.Single(map.Walls);
        Assert.Empty(map.Regions);
    }
}
