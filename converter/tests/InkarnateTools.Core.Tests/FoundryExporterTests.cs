using System.Text;
using System.Text.Json;
using InkarnateTools.Composition;
using InkarnateTools.Core.Export;
using InkarnateTools.Core.Geometry;
using InkarnateTools.Core.Models;
using InkarnateTools.Inkarnate;
using Xunit;

namespace InkarnateTools.Core.Tests;

public class WallExportRunTests
{
    [Fact]
    public void BuildExportRuns_GappedStraightWall_ProducesThreeOrderedRuns()
    {
        var wall = new Wall
        {
            EntityId = 5,
            Origin = new MapPoint(0, 0),
            Scale = 1,
            Points =
            [
                new MapPoint(0, 0),
                new MapPoint(1000, 0),
            ],
            Portals =
            [
                new WallPortal
                {
                    Anchor = new MapPoint(500, 0),
                    Width = 200,
                    LineType = WallLineType.Door,
                },
            ],
        };

        var runs = WallPathSegmentBuilder.BuildExportRuns(wall);

        Assert.Equal(3, runs.Count);
        Assert.Equal(WallLineType.Default, runs[0].LineType);
        Assert.Equal(WallLineType.Door, runs[1].LineType);
        Assert.Equal(WallLineType.Default, runs[2].LineType);
        Assert.Equal(2, runs[0].Points.Count);
        Assert.Equal(2, runs[1].Points.Count);
        Assert.Equal(2, runs[2].Points.Count);
    }

    [Fact]
    public async Task BuildExportRuns_BasicWallsGappedWall_ProducesThreeRuns()
    {
        var map = await LoadBasicWallsAsync();
        var gappedWall = map.Walls.Single(wall => wall.EntityId == 5);

        var runs = WallPathSegmentBuilder.BuildExportRuns(gappedWall);

        Assert.Equal(3, runs.Count);
        Assert.Single(runs, run => run.LineType == WallLineType.Door);
    }

    private static async Task<MapDocument> LoadBasicWallsAsync()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "basic-walls.ink"));
        await using var input = File.OpenRead(path);
        return await new InkarnateImporter().ImportAsync(input);
    }
}

public class FoundryExporterTests
{
    private static string BasicWallsInkPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "basic-walls.ink"));

    [Fact]
    public async Task ExportAsync_WritesFoundryScene_WithExpectedMetadata()
    {
        var map = await LoadPreparedBasicWallsMapAsync();
        await using var output = new MemoryStream();

        await ServiceFactory.CreateConvertMapService()
            .ConvertAsync(map, output, "foundry");

        output.Position = 0;
        using var document = await JsonDocument.ParseAsync(output);

        Assert.Equal(map.Name, document.RootElement.GetProperty("name").GetString());
        Assert.Equal(2048, document.RootElement.GetProperty("width").GetInt32());
        Assert.Equal(1536, document.RootElement.GetProperty("height").GetInt32());
        Assert.Equal(
            "basic-walls.webp",
            document.RootElement.GetProperty("levels")[0]
                .GetProperty("background")
                .GetProperty("src")
                .GetString());

        var walls = document.RootElement.GetProperty("walls");
        Assert.True(walls.GetArrayLength() > 0);

        output.Position = 0;
        var json = await ReadStreamAsStringAsync(output);
        Assert.DoesNotContain("Placeholder export", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportAsync_StraightWall_ProducesSingleSegment()
    {
        var map = await LoadPreparedBasicWallsMapAsync();
        var transform = SceneTransform.FromMap(map)!;
        var straightWall = map.Walls.Single(wall => wall.EntityId == 3);

        var segments = FoundryWallSegmentBuilder.BuildFromWall(straightWall, transform);

        Assert.Single(segments);
        Assert.Equal(0, segments[0].Door);
        Assert.Equal(20, segments[0].Sight);
    }

    [Fact]
    public async Task ExportAsync_GappedWall_ProducesThreeSegmentsWithOneDoor()
    {
        var map = await LoadPreparedBasicWallsMapAsync();
        var transform = SceneTransform.FromMap(map)!;
        var gappedWall = map.Walls.Single(wall => wall.EntityId == 5);

        var segments = FoundryWallSegmentBuilder.BuildFromWall(gappedWall, transform);

        Assert.Equal(3, segments.Count);
        Assert.Single(segments, segment => segment.Door == 1);
        Assert.Equal(2, segments.Count(segment => segment.Door == 0));
    }

    [Fact]
    public async Task ExportAsync_ClosedTerrainWall_ProducesEightTerrainSegments()
    {
        var map = await LoadPreparedBasicWallsMapAsync();
        var transform = SceneTransform.FromMap(map)!;
        var terrainWall = map.Walls.Single(wall => wall.EntityId == 6);

        var segments = FoundryWallSegmentBuilder.BuildFromWall(terrainWall, transform);

        Assert.Equal(8, segments.Count);
        Assert.All(segments, segment => Assert.Equal(10, segment.Sight));
    }

    [Fact]
    public async Task ExportAsync_FullMap_HasExactlyOneDoorSegment()
    {
        var map = await LoadPreparedBasicWallsMapAsync();
        var segments = FoundryWallSegmentBuilder.BuildFromMap(map);

        Assert.Single(segments, segment => segment.Door == 1);
        Assert.Equal(8, segments.Count(segment => segment.Sight == 10));
    }

    private static async Task<MapDocument> LoadPreparedBasicWallsMapAsync()
    {
        Assert.True(File.Exists(BasicWallsInkPath), $"Missing test resource: {BasicWallsInkPath}");

        await using var input = File.OpenRead(BasicWallsInkPath);
        var map = await new InkarnateImporter().ImportAsync(input);
        map.SourceFileName = "basic-walls.ink";
        map.Walls.Single(wall => wall.EntityId == 6).LineType = WallLineType.Terrain;
        return map;
    }

    private static async Task<string> ReadStreamAsStringAsync(MemoryStream stream)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}
