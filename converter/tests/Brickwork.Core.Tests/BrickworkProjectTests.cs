using System.Text.Json;
using Brickwork.Core.Editing;
using Brickwork.Core.Models;
using Brickwork.Inkarnate;
using Xunit;

namespace Brickwork.Core.Tests;

public class BrickworkProjectTests
{
    private static readonly string MassEditsMapPath =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "test-maps", "map-with-mass-edits.ink"));

    private static readonly string RegionsMapPath =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "test-maps", "basic-regions.ink"));

    [Fact]
    public async Task SaveAndLoad_KeepsTheMapAndTheSource()
    {
        var sourceBytes = await File.ReadAllBytesAsync(RegionsMapPath);
        var map = await new InkarnateImporter().ImportAsync(new MemoryStream(sourceBytes));
        MapReimportMerger.RecordImportedIds(map);
        map.SceneLevelCount = 3;
        map.Walls[0].IsActive = false;
        map.Walls[0].Levels = [2];
        map.Layers[0].Name = "Renamed in Brickwork";

        using var file = new MemoryStream();
        BrickworkProject.Save(new BrickworkProject { Map = map, SourceFileName = "basic-regions.ink", SourceBytes = sourceBytes }, file);
        file.Position = 0;
        var loaded = BrickworkProject.Load(file);

        Assert.Equal("basic-regions.ink", loaded.SourceFileName);
        Assert.Equal(sourceBytes, loaded.SourceBytes);
        Assert.Equal(map.PreviewImagePng, loaded.Map.PreviewImagePng);
        map.Compatibility = null;
        loaded.Map.Compatibility = null;
        Assert.Equal(Json(map), Json(loaded.Map));
        Assert.Equal([2], loaded.Map.Walls[0].Levels);
    }

    [Fact]
    public async Task UpdateToTheSameBackup_ChangesNothing()
    {
        async Task<MapDocument> Import()
        {
            await using var input = File.OpenRead(MassEditsMapPath);
            var imported = await new InkarnateImporter().ImportAsync(input);
            MapReimportMerger.RecordImportedIds(imported);
            return imported;
        }

        var project = await Import();
        project.Walls[0].IsActive = false;
        project.Walls[1].Levels = [1, 3];
        project.Walls.RemoveAt(project.Walls.Count - 1);
        project.Compatibility = null;
        var expected = Json(project).Replace(project.Id.ToString(), "id");

        var result = ProjectUpdateMerger.Merge(project, await Import(), await Import()).Map;
        result.Compatibility = null;

        Assert.Equal(expected, Json(result).Replace(result.Id.ToString(), "id"));
    }

    [Fact]
    public void Load_RejectsOtherZipFiles()
    {
        using var file = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(file, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            zip.CreateEntry("something.txt");
        }

        file.Position = 0;
        Assert.Throws<InvalidDataException>(() => BrickworkProject.Load(file));
    }

    private static string Json(MapDocument map) =>
        JsonSerializer.Serialize(map, new JsonSerializerOptions { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } });
}
