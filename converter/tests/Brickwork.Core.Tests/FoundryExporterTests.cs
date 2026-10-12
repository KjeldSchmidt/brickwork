using System.Text;
using System.Text.Json;
using Brickwork.Composition;
using Brickwork.Core.Export;
using Brickwork.Core.Geometry;
using Brickwork.Core.Models;
using Brickwork.Inkarnate;
using Xunit;

namespace Brickwork.Core.Tests;

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
        Assert.Equal(WallLineType.Solid, runs[0].LineType);
        Assert.Equal(WallLineType.Door, runs[1].LineType);
        Assert.Equal(WallLineType.Solid, runs[2].LineType);
        Assert.Equal(2, runs[0].Points.Count);
        Assert.Equal(2, runs[1].Points.Count);
        Assert.Equal(2, runs[2].Points.Count);
    }

    [Fact]
    public void BuildExportRuns_InactivePortal_LeavesGapWithoutPortalRun()
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
                    IsActive = false,
                },
            ],
        };

        var runs = WallPathSegmentBuilder.BuildExportRuns(wall);

        Assert.Equal(2, runs.Count);
        Assert.All(runs, run => Assert.False(run.IsPortal));
    }

    [Fact]
    public void BuildExportRuns_DisabledPortal_LeavesGapWithoutPortalRun()
    {
        var wall = new Wall
        {
            EntityId = 5,
            Origin = new MapPoint(0, 0),
            Scale = 1,
            LineType = WallLineType.Solid,
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
                    LineType = WallLineType.Disabled,
                    // Stale flag as if type was set without syncing IsActive.
                    IsActive = true,
                },
            ],
        };

        var runs = WallPathSegmentBuilder.BuildExportRuns(wall);

        Assert.Equal(2, runs.Count);
        Assert.All(runs, run => Assert.False(run.IsPortal));
        Assert.All(runs, run => Assert.Equal(WallLineType.Solid, run.LineType));
    }

    [Fact]
    public void BuildExportRuns_InactiveWallWithActivePortal_StillProducesPortalRun()
    {
        var wall = new Wall
        {
            EntityId = 5,
            IsActive = false,
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
                    IsActive = true,
                },
            ],
        };

        var runs = WallPathSegmentBuilder.BuildExportRuns(wall);

        Assert.Equal(3, runs.Count);
        Assert.Single(runs, run => run.IsPortal);
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

    [Fact]
    public void BuildExportRuns_ClosedWithOneGap_MergesIntoSingleWallRunAroundSeam()
    {
        var wall = new Wall
        {
            EntityId = 1,
            IsClosed = true,
            Origin = new MapPoint(0, 0),
            PathOrigin = new MapPoint(0, 0),
            Scale = 1,
            LineType = WallLineType.Solid,
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
                    Anchor = new MapPoint(50, 0),
                    Width = 20,
                    LineType = WallLineType.Door,
                },
            ],
        };

        var runs = WallPathSegmentBuilder.BuildExportRuns(wall);

        Assert.Equal(2, runs.Count);
        Assert.Equal(WallLineType.Solid, runs[0].LineType);
        Assert.Equal(WallLineType.Door, runs[1].LineType);
        Assert.True(runs[0].Points.Count >= 4);
    }

    [Fact]
    public void BuildExportRuns_ClosedRectangle_ProducesFourWallSegments()
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
        };

        var runs = WallPathSegmentBuilder.BuildExportRuns(wall);

        var run = Assert.Single(runs);
        Assert.Equal(5, run.Points.Count);
        Assert.Equal(4, run.Points.Count - 1);
    }

    [Fact]
    public async Task BuildExportRuns_PrisonBars_ProducesFourFoundrySegments()
    {
        var map = await LoadMassEditsMapAsync();
        var transform = SceneTransform.FromMap(map)!;
        var prisonBar = map.Walls.First(wall =>
            wall.EntityId == 11653 &&
            (wall.Name ?? string.Empty).Contains("Prison Bars", StringComparison.OrdinalIgnoreCase));

        var segments = FoundryWallSegmentBuilder.BuildFromWall(prisonBar, transform);

        Assert.Equal(4, segments.Count);
    }

    [Fact]
    public async Task BuildExportRuns_Entity1646_IncludesClosingSegment()
    {
        var map = await LoadMassEditsMapAsync();
        var transform = SceneTransform.FromMap(map)!;
        var wall = map.Walls.Single(candidate => candidate.EntityId == 1646);

        var segments = FoundryWallSegmentBuilder.BuildFromWall(wall, transform);

        Assert.Equal(wall.Points.Count, segments.Count);
    }

    private static async Task<MapDocument> LoadMassEditsMapAsync()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "test-maps", "map-with-mass-edits.ink"));
        await using var input = File.OpenRead(path);
        return await new InkarnateImporter().ImportAsync(input);
    }

    private static async Task<MapDocument> LoadBasicWallsAsync()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "test-maps", "basic-walls.ink"));
        await using var input = File.OpenRead(path);
        return await new InkarnateImporter().ImportAsync(input);
    }
}

public class FoundryExporterTests
{
    private static string BasicWallsInkPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "test-maps", "basic-walls.ink"));

    [Fact]
    public async Task ExportAsync_WritesFoundryScene_WithExpectedMetadata()
    {
        var map = await LoadPreparedBasicWallsMapAsync();
        await using var output = new MemoryStream();

        await ServiceFactory.CreateConvertMapService()
            .ConvertAsync(map, output, "foundry");

        output.Position = 0;
        using var document = await JsonDocument.ParseAsync(output);

        var expectedCanvas = FoundryExportCanvas.FromMap(map);

        Assert.Equal(map.Name, document.RootElement.GetProperty("name").GetString());
        Assert.Equal(expectedCanvas.Width, document.RootElement.GetProperty("width").GetInt32());
        Assert.Equal(expectedCanvas.Height, document.RootElement.GetProperty("height").GetInt32());
        Assert.Equal(
            expectedCanvas.GridSize,
            document.RootElement.GetProperty("grid").GetProperty("size").GetInt32());
        // Legacy root background.src — V14 migrateLevels copies this onto levels[0] on import.
        Assert.Equal(
            "basic-walls.webp",
            document.RootElement.GetProperty("background").GetProperty("src").GetString());
        Assert.Equal(
            "basic-walls.webp",
            document.RootElement.GetProperty("thumb").GetString());
        Assert.False(document.RootElement.TryGetProperty("_stats", out _));

        var walls = document.RootElement.GetProperty("walls");
        Assert.True(walls.GetArrayLength() > 0);

        output.Position = 0;
        var json = await ReadStreamAsStringAsync(output);
        Assert.DoesNotContain("Placeholder export", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportAsync_WithUnderlyingMap_UsesImageDimensionsAndGridSize()
    {
        var map = await LoadPreparedBasicWallsMapAsync();
        Assert.True(map.Grid.Columns > 0);
        map.UnderlyingMap = new UnderlyingMapInfo
        {
            FileName = "basic-walls.png",
            AbsolutePath = @"C:\maps\basic-walls.png",
            Width = map.Grid.Columns * 120,
            Height = map.Grid.Rows * 120,
        };

        await using var output = new MemoryStream();
        await ServiceFactory.CreateConvertMapService()
            .ConvertAsync(map, output, "foundry");

        output.Position = 0;
        using var document = await JsonDocument.ParseAsync(output);

        Assert.Equal(map.Grid.Columns * 120, document.RootElement.GetProperty("width").GetInt32());
        Assert.Equal(map.Grid.Rows * 120, document.RootElement.GetProperty("height").GetInt32());
        Assert.Equal(120, document.RootElement.GetProperty("grid").GetProperty("size").GetInt32());

        var segments = FoundryWallSegmentBuilder.BuildFromMap(map);
        var canvas = FoundryExportCanvas.FromMap(map);
        Assert.All(segments, segment =>
        {
            Assert.InRange(segment.X0, 0, canvas.Width);
            Assert.InRange(segment.Y0, 0, canvas.Height);
            Assert.InRange(segment.X1, 0, canvas.Width);
            Assert.InRange(segment.Y1, 0, canvas.Height);
        });
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
    public async Task ExportAsync_ClosedTerrainWall_ProducesOuterAndInnerTerrainSegments()
    {
        var map = await LoadPreparedBasicWallsMapAsync();
        var transform = SceneTransform.FromMap(map)!;
        var terrainWall = map.Walls.Single(wall => wall.EntityId == 6);

        var ring = WallThicknessPolygonBuilder.BuildClosedRing(terrainWall.Points, terrainWall.SceneThickness);
        Assert.NotNull(ring);

        var segments = FoundryWallSegmentBuilder.BuildFromWall(terrainWall, transform);

        Assert.Equal(12, segments.Count);
        Assert.All(segments, segment => Assert.Equal(10, segment.Sight));

        var outerMaxX = ring!.Outer.Max(point => transform.SceneToPreview(point).X);
        var innerMaxX = ring.Inner.Max(point => transform.SceneToPreview(point).X);
        Assert.True(outerMaxX > innerMaxX);
        Assert.Contains(
            segments,
            segment => segment.X0 == (int)Math.Round(outerMaxX) || segment.X1 == (int)Math.Round(outerMaxX));
        Assert.Contains(
            segments,
            segment => segment.X0 == (int)Math.Round(innerMaxX) || segment.X1 == (int)Math.Round(innerMaxX));
    }

    [Fact]
    public async Task ExportAsync_OpenTerrainWall_ProducesOutlinePolygonSegments()
    {
        var map = await LoadPreparedBasicWallsMapAsync();
        var transform = SceneTransform.FromMap(map)!;
        var terrainWall = map.Walls.Single(wall => wall.EntityId == 3);
        terrainWall.LineType = WallLineType.Terrain;

        var loops = WallThicknessPolygonBuilder.BuildTerrainExportLoops(
            terrainWall.Points,
            terrainWall.SceneThickness,
            terrainWall.IsClosed);
        var outline = Assert.Single(loops);

        var segments = FoundryWallSegmentBuilder.BuildFromWall(terrainWall, transform);

        Assert.Equal(outline.Count, segments.Count);
        Assert.All(segments, segment => Assert.Equal(10, segment.Sight));
        Assert.DoesNotContain(segments, segment => segment.Door == 1);
    }

    [Fact]
    public async Task ExportAsync_ClosedTerrainWallWithInactivePortal_SplitsTerrainPolygonsAtGap()
    {
        var map = await LoadPreparedBasicWallsMapAsync();
        var transform = SceneTransform.FromMap(map)!;
        var terrainWall = map.Walls.Single(wall => wall.EntityId == 6);

        var intactSegments = FoundryWallSegmentBuilder.BuildFromWall(terrainWall, transform);
        Assert.Equal(12, intactSegments.Count);

        terrainWall.Portals.Add(new WallPortal
        {
            Anchor = new MapPoint(50, 0),
            Width = 20,
            LineType = WallLineType.Door,
            IsActive = false,
        });

        var pathSegments = WallPathSegmentBuilder.BuildSegments(terrainWall);
        Assert.Single(pathSegments);
        Assert.False(pathSegments[0].IsClosed);

        var segments = FoundryWallSegmentBuilder.BuildFromWall(terrainWall, transform);
        Assert.NotEqual(intactSegments.Count, segments.Count);
        Assert.All(segments, segment => Assert.Equal(10, segment.Sight));
        Assert.DoesNotContain(segments, segment => segment.Door == 1);
    }

    [Fact]
    public async Task ExportAsync_InactiveWallWithActivePortal_ExportsDoorOnly()
    {
        var map = await LoadPreparedBasicWallsMapAsync();
        var transform = SceneTransform.FromMap(map)!;
        var gappedWall = map.Walls.Single(wall => wall.EntityId == 5);
        gappedWall.IsActive = false;
        foreach (var portal in gappedWall.Portals)
        {
            portal.IsActive = true;
        }

        var segments = FoundryWallSegmentBuilder.BuildFromWall(gappedWall, transform);

        Assert.Single(segments);
        Assert.Equal(1, segments[0].Door);
    }

    [Fact]
    public async Task ExportAsync_ActiveWallWithInactivePortal_ExportsSplitWallWithoutDoor()
    {
        var map = await LoadPreparedBasicWallsMapAsync();
        var transform = SceneTransform.FromMap(map)!;
        var gappedWall = map.Walls.Single(wall => wall.EntityId == 5);
        gappedWall.IsActive = true;
        foreach (var portal in gappedWall.Portals)
        {
            portal.IsActive = false;
        }

        var segments = FoundryWallSegmentBuilder.BuildFromWall(gappedWall, transform);

        Assert.Equal(2, segments.Count);
        Assert.All(segments, segment => Assert.Equal(0, segment.Door));
    }

    [Fact]
    public async Task ExportAsync_FullMap_HasExactlyOneDoorSegment()
    {
        var map = await LoadPreparedBasicWallsMapAsync();
        var segments = FoundryWallSegmentBuilder.BuildFromMap(map);

        Assert.Single(segments, segment => segment.Door == 1);
        Assert.Equal(12, segments.Count(segment => segment.Sight == 10));
    }

    [Fact]
    public async Task ExportAsync_UsesFoundryMapsDirectory_ForBackgroundSrc()
    {
        var map = await LoadPreparedBasicWallsMapAsync();
        var data = Path.Combine(Path.GetTempPath(), "brickwork-foundry-export-" + Guid.NewGuid().ToString("N"), "Data");
        var maps = Path.Combine(data, "maps");
        Directory.CreateDirectory(maps);
        map.FoundryMapsDirectory = maps;

        try
        {
            await using var output = new MemoryStream();
            await ServiceFactory.CreateConvertMapService()
                .ConvertAsync(map, output, "foundry");

            output.Position = 0;
            using var document = await JsonDocument.ParseAsync(output);
            Assert.Equal(
                "maps/basic-walls.webp",
                document.RootElement.GetProperty("background").GetProperty("src").GetString());
            Assert.Equal(
                "maps/basic-walls.webp",
                document.RootElement.GetProperty("thumb").GetString());
        }
        finally
        {
            try
            {
                Directory.Delete(Path.GetDirectoryName(data)!, recursive: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    [Fact]
    public async Task ExportAsync_GroupsRegionsByType_WithExpectedBehaviors()
    {
        var map = await LoadPreparedBasicWallsMapAsync();
        map.Regions.Add(CreateRegion(101, RegionType.DifficultTerrain, (0, 0), (100, 0), (100, 100)));
        map.Regions.Add(CreateRegion(102, RegionType.DifficultTerrain, (200, 0), (300, 0), (300, 100)));
        map.Regions.Add(CreateRegion(103, RegionType.GreaterDifficultTerrain, (0, 200), (100, 200), (50, 300)));
        map.Regions.Add(CreateRegion(104, RegionType.Other, (400, 0), (500, 0), (500, 100)));
        map.Regions.Add(CreateRegion(105, RegionType.DifficultTerrain, (0, 0), (10, 0), (10, 10), isActive: false));

        await using var output = new MemoryStream();
        await ServiceFactory.CreateConvertMapService()
            .ConvertAsync(map, output, "foundry");

        output.Position = 0;
        using var document = await JsonDocument.ParseAsync(output);
        var regions = document.RootElement.GetProperty("regions");
        Assert.Equal(3, regions.GetArrayLength());

        var difficult = Assert.Single(regions.EnumerateArray(), r => r.GetProperty("name").GetString() == "Difficult Terrain");
        Assert.Equal(2, difficult.GetProperty("shapes").GetArrayLength());
        Assert.All(difficult.GetProperty("shapes").EnumerateArray(), shape => Assert.Equal("polygon", shape.GetProperty("type").GetString()));
        var difficultCost = difficult.GetProperty("behaviors")[0].GetProperty("system").GetProperty("difficulties");
        Assert.Equal(2, difficultCost.GetProperty("walk").GetInt32());
        Assert.Equal(2, difficultCost.GetProperty("deploy").GetInt32());

        var greater = Assert.Single(regions.EnumerateArray(), r => r.GetProperty("name").GetString() == "Greater Difficult Terrain");
        Assert.Equal(1, greater.GetProperty("shapes").GetArrayLength());
        Assert.Equal(3, greater.GetProperty("behaviors")[0].GetProperty("system").GetProperty("difficulties").GetProperty("walk").GetInt32());

        var other = Assert.Single(regions.EnumerateArray(), r => r.GetProperty("name").GetString() == "Other");
        Assert.Equal(1, other.GetProperty("shapes").GetArrayLength());
        Assert.Equal(0, other.GetProperty("behaviors").GetArrayLength());
        Assert.Equal("#1aa3a3", other.GetProperty("color").GetString());
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

    private static Region CreateRegion(
        int entityId,
        RegionType regionType,
        (double X, double Y) a,
        (double X, double Y) b,
        (double X, double Y) c,
        bool isActive = true) =>
        new()
        {
            EntityId = entityId,
            RegionType = regionType,
            IsActive = isActive,
            Points =
            {
                new MapPoint(a.X, a.Y),
                new MapPoint(b.X, b.Y),
                new MapPoint(c.X, c.Y),
            },
        };

    private static async Task<string> ReadStreamAsStringAsync(MemoryStream stream)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}

public class FoundryRegionBuilderTests
{
    [Fact]
    public void BuildFromMap_GroupsByType_AndSkipsInactive()
    {
        var map = CreateMap();
        map.Regions.Add(CreateRegion(1, RegionType.DifficultTerrain, (0, 0), (10, 0), (10, 10)));
        map.Regions.Add(CreateRegion(2, RegionType.DifficultTerrain, (20, 0), (30, 0), (30, 10)));
        map.Regions.Add(CreateRegion(3, RegionType.Other, (0, 20), (10, 20), (10, 30)));
        map.Regions.Add(CreateRegion(4, RegionType.GreaterDifficultTerrain, (0, 0), (5, 0), (5, 5), isActive: false));

        var regions = FoundryRegionBuilder.BuildFromMap(map);

        Assert.Equal(2, regions.Count);
        var difficult = Assert.Single(regions, r => r.Name == "Difficult Terrain");
        Assert.Equal(2, difficult.Shapes.Count);
        Assert.Equal(2, difficult.WalkMovementCost);
        var other = Assert.Single(regions, r => r.Name == "Other");
        Assert.Null(other.WalkMovementCost);
        Assert.DoesNotContain(regions, r => r.Name == "Greater Difficult Terrain");
    }

    [Fact]
    public void BuildFromMap_TransformsScenePointsToPreviewIntegers()
    {
        var map = CreateMap(sceneWidth: 100, sceneHeight: 100, previewWidth: 200, previewHeight: 200);
        map.Regions.Add(CreateRegion(1, RegionType.DifficultTerrain, (10, 20), (30, 20), (30, 40)));

        var region = Assert.Single(FoundryRegionBuilder.BuildFromMap(map));
        var points = Assert.Single(region.Shapes).Points;

        Assert.Equal([20, 40, 60, 40, 60, 80], points);
    }

    [Fact]
    public void BuildFromMap_WithGrid_ScalesToFoundryCanvas()
    {
        var map = CreateMap(sceneWidth: 100, sceneHeight: 100, previewWidth: 200, previewHeight: 200);
        map.Grid = new GridInfo { Columns = 10, Rows = 10, CellSize = 10 };
        map.Regions.Add(CreateRegion(1, RegionType.DifficultTerrain, (10, 20), (30, 20), (30, 40)));

        var region = Assert.Single(FoundryRegionBuilder.BuildFromMap(map));
        var points = Assert.Single(region.Shapes).Points;

        // Canvas is 10*100 × 10*100 = 1000×1000 (not preview 200×200).
        Assert.Equal([100, 200, 300, 200, 300, 400], points);
    }

    private static MapDocument CreateMap(
        double sceneWidth = 1000,
        double sceneHeight = 1000,
        int previewWidth = 1000,
        int previewHeight = 1000) =>
        new()
        {
            Scene = new SceneDimensions { Width = sceneWidth, Height = sceneHeight },
            Preview = new PreviewDimensions { Width = previewWidth, Height = previewHeight },
        };

    private static Region CreateRegion(
        int entityId,
        RegionType regionType,
        (double X, double Y) a,
        (double X, double Y) b,
        (double X, double Y) c,
        bool isActive = true) =>
        new()
        {
            EntityId = entityId,
            RegionType = regionType,
            IsActive = isActive,
            Points =
            {
                new MapPoint(a.X, a.Y),
                new MapPoint(b.X, b.Y),
                new MapPoint(c.X, c.Y),
            },
        };
}
