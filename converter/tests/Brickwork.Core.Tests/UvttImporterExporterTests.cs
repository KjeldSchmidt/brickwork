using System.Text.Json;
using Brickwork.Composition;
using Brickwork.Core.Models;
using Brickwork.Exporters.Uvtt;
using Xunit;

namespace Brickwork.Core.Tests;

public class UvttImporterExporterTests
{
    private static string PigAndWhistlePath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "The Pig and Whistle tavern.uvtt"));

    private static string EmptyBackupInkPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "empty-backup.ink"));

    [Fact]
    public async Task ImportAsync_LoadsPigAndWhistleMetadata()
    {
        Assert.True(File.Exists(PigAndWhistlePath), $"Missing test resource: {PigAndWhistlePath}");

        var importer = new UvttImporter();
        await using var input = File.OpenRead(PigAndWhistlePath);

        var map = await importer.ImportAsync(input);

        Assert.Equal(32, map.Grid.Columns);
        Assert.Equal(22, map.Grid.Rows);
        Assert.Equal(120, map.Grid.PixelsPerCell);
        Assert.Equal(1, map.Grid.CellSize);
        Assert.Equal(32, map.Scene.Width);
        Assert.Equal(22, map.Scene.Height);
        Assert.NotNull(map.Preview);
        Assert.NotNull(map.PreviewImagePng);
        Assert.True(map.PreviewImagePng!.Length > 0);
        Assert.Equal(0x89, map.PreviewImagePng[0]);
        Assert.Empty(map.Lights);
        Assert.Equal(58, map.Walls.Count(w => w.LineType == WallLineType.Window));
        Assert.Equal(12, map.Walls.Count(w => w.LineType == WallLineType.Door));
        Assert.Equal(0, map.Walls.Count(w => w.LineType == WallLineType.Solid));
    }

    [Fact]
    public async Task ExportAsync_WritesExpectedBuckets_ForSyntheticMap()
    {
        var map = new MapDocument
        {
            Name = "Synthetic",
            Scene = new SceneDimensions { Width = 10, Height = 10 },
            Preview = new PreviewDimensions { Width = 700, Height = 700 },
            PreviewImagePng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 2],
            Grid = new GridInfo
            {
                CellSize = 1,
                PixelsPerCell = 70,
                Columns = 10,
                Rows = 10,
            },
            Walls =
            [
                CreateWall(1, WallLineType.Solid, [new MapPoint(0, 0), new MapPoint(5, 0)]),
                CreateWall(2, WallLineType.Window, [new MapPoint(1, 1), new MapPoint(4, 1)]),
                CreateWall(3, WallLineType.Ethereal, [new MapPoint(2, 2), new MapPoint(6, 2)]),
                CreateWall(4, WallLineType.Door, [new MapPoint(3, 3), new MapPoint(3, 6)]),
                CreateWall(5, WallLineType.SecretDoor, [new MapPoint(4, 4), new MapPoint(7, 4)]),
            ],
        };

        var exporter = new UvttExporter();
        await using var output = new MemoryStream();
        await exporter.ExportAsync(map, output);

        output.Position = 0;
        using var document = await JsonDocument.ParseAsync(output);
        var root = document.RootElement;

        Assert.Equal(1.0, root.GetProperty("format").GetDouble());
        Assert.Equal("Brickwork", root.GetProperty("software").GetString());
        Assert.Equal(1, root.GetProperty("line_of_sight").GetArrayLength());
        Assert.Equal(2, root.GetProperty("objects_line_of_sight").GetArrayLength());
        Assert.Equal(2, root.GetProperty("portals").GetArrayLength());
        Assert.Equal(0, root.GetProperty("lights").GetArrayLength());
        Assert.False(root.GetProperty("environment").GetProperty("baked_lighting").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("image").GetString()));
    }

    [Fact]
    public async Task RoundTrip_PreservesWallTypeBuckets()
    {
        var original = new MapDocument
        {
            Name = "RoundTrip",
            Scene = new SceneDimensions { Width = 8, Height = 8 },
            Preview = new PreviewDimensions { Width = 560, Height = 560 },
            PreviewImagePng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 2],
            Grid = new GridInfo
            {
                CellSize = 1,
                PixelsPerCell = 70,
                Columns = 8,
                Rows = 8,
            },
            Walls =
            [
                CreateWall(1, WallLineType.Solid, [new MapPoint(0, 0), new MapPoint(2, 0)]),
                CreateWall(2, WallLineType.Window, [new MapPoint(0, 2), new MapPoint(2, 2)]),
                CreateWall(3, WallLineType.Door, [new MapPoint(4, 4), new MapPoint(6, 4)]),
            ],
        };

        var exporter = new UvttExporter();
        await using var exported = new MemoryStream();
        await exporter.ExportAsync(original, exported);

        exported.Position = 0;
        var importer = new UvttImporter();
        var roundTripped = await importer.ImportAsync(exported);

        Assert.Equal(8, roundTripped.Grid.Columns);
        Assert.Equal(8, roundTripped.Grid.Rows);
        Assert.Equal(70, roundTripped.Grid.PixelsPerCell);
        Assert.Equal(1, roundTripped.Walls.Count(w => w.LineType == WallLineType.Solid));
        Assert.Equal(1, roundTripped.Walls.Count(w => w.LineType == WallLineType.Window));
        Assert.Equal(1, roundTripped.Walls.Count(w => w.LineType == WallLineType.Door));
    }

    [Fact]
    public async Task ConvertAsync_WritesUvtt1_FromInkBackup()
    {
        Assert.True(File.Exists(EmptyBackupInkPath), $"Missing test resource: {EmptyBackupInkPath}");

        var service = ServiceFactory.CreateConvertMapService();
        await using var input = File.OpenRead(EmptyBackupInkPath);
        await using var output = new MemoryStream();

        await service.ConvertAsync(input, output, "uvtt1", "empty-backup.ink");

        output.Position = 0;
        using var document = await JsonDocument.ParseAsync(output);
        var root = document.RootElement;

        Assert.Equal(1.0, root.GetProperty("format").GetDouble());
        Assert.True(root.TryGetProperty("image", out var image));
        Assert.False(string.IsNullOrWhiteSpace(image.GetString()));
        Assert.Equal(40, root.GetProperty("resolution").GetProperty("map_size").GetProperty("x").GetDouble());
        Assert.Equal(30, root.GetProperty("resolution").GetProperty("map_size").GetProperty("y").GetDouble());
    }

    [Fact]
    public void IsUvttPath_RecognizesSupportedExtensions()
    {
        Assert.True(UvttImporter.IsUvttPath("map.uvtt"));
        Assert.True(UvttImporter.IsUvttPath("map.dd2vtt"));
        Assert.True(UvttImporter.IsUvttPath("map.df2vtt"));
        Assert.False(UvttImporter.IsUvttPath("map.ink"));
    }

    private static Wall CreateWall(int entityId, WallLineType lineType, IReadOnlyList<MapPoint> points)
    {
        var wall = new Wall
        {
            EntityId = entityId,
            LineType = lineType,
            IsActive = true,
            WallEnabled = true,
        };

        foreach (var point in points)
        {
            wall.Points.Add(point);
        }

        return wall;
    }
}
