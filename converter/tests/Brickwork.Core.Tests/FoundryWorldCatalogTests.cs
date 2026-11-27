using System.Text.Json.Nodes;
using Brickwork.Core.Models;
using Brickwork.Exporters.Foundry;
using LevelDB;
using Xunit;

namespace Brickwork.Core.Tests;

public class FoundryWorldCatalogTests
{
    [Fact]
    public void ListWorlds_ReadsWorldJson()
    {
        using var temp = new TempDir();
        var data = Path.Combine(temp.Path, "Data");
        var worldDir = Path.Combine(data, "worlds", "demo-world");
        Directory.CreateDirectory(worldDir);
        File.WriteAllText(
            Path.Combine(worldDir, "world.json"),
            """{"id":"demo-world","title":"Demo World"}""");

        var catalog = new FoundryWorldCatalog();
        var worlds = catalog.ListWorlds(data);

        var world = Assert.Single(worlds);
        Assert.Equal("demo-world", world.Id);
        Assert.Equal("Demo World", world.Title);
    }

    [Fact]
    public void ListScenes_AndLoad_FromNedb()
    {
        using var temp = new TempDir();
        var data = Path.Combine(temp.Path, "Data");
        var worldData = Path.Combine(data, "worlds", "legacy", "data");
        Directory.CreateDirectory(worldData);
        File.WriteAllText(
            Path.Combine(data, "worlds", "legacy", "world.json"),
            """{"id":"legacy","title":"Legacy"}""");

        var scene = new JsonObject
        {
            ["_id"] = "scene1",
            ["name"] = "Town Square",
            ["width"] = 1000,
            ["height"] = 800,
            ["grid"] = new JsonObject { ["size"] = 100 },
            ["walls"] = new JsonArray(
                new JsonObject
                {
                    ["_id"] = "w1",
                    ["c"] = new JsonArray(0, 0, 100, 0),
                    ["sight"] = 20,
                    ["light"] = 20,
                    ["sound"] = 20,
                    ["move"] = 20,
                    ["door"] = 0,
                }),
            ["regions"] = new JsonArray(),
        };
        File.WriteAllText(Path.Combine(worldData, "scenes.db"), scene.ToJsonString() + Environment.NewLine);

        var catalog = new FoundryWorldCatalog();
        var summaries = catalog.ListScenes(data, "legacy");
        var summary = Assert.Single(summaries);
        Assert.Equal("scene1", summary.Id);
        Assert.Equal("Town Square", summary.Name);
        Assert.Equal(1, summary.WallCount);

        var assembled = catalog.LoadAssembledScene(data, "legacy", "scene1");
        Assert.Equal("Town Square", assembled["name"]!.GetValue<string>());

        var importer = new FoundryWorldSceneImporter(catalog);
        var map = importer.ImportScene(data, "legacy", "scene1");
        Assert.Equal("Town Square", map.Name);
        Assert.Equal("foundry:legacy/scene1", map.SourceFileName);
        Assert.Single(map.Walls);
    }

    [Fact]
    public void ListScenes_AndLoad_FromLevelDb()
    {
        using var temp = new TempDir();
        var data = Path.Combine(temp.Path, "Data");
        var worldData = Path.Combine(data, "worlds", "ldb-world", "data");
        var scenesDb = Path.Combine(worldData, "scenes");
        Directory.CreateDirectory(scenesDb);
        File.WriteAllText(
            Path.Combine(data, "worlds", "ldb-world", "world.json"),
            """{"id":"ldb-world","title":"LevelDB World"}""");

        SeedLevelDb(scenesDb);

        var catalog = new FoundryWorldCatalog();
        var summaries = catalog.ListScenes(data, "ldb-world");
        var summary = Assert.Single(summaries);
        Assert.Equal("sc01", summary.Id);
        Assert.Equal("Seed Scene", summary.Name);
        Assert.Equal(1, summary.WallCount);
        Assert.Equal(1, summary.RegionCount);

        var map = new FoundryWorldSceneImporter(catalog).ImportScene(data, "ldb-world", "sc01");
        Assert.Equal("Seed Scene", map.Name);
        Assert.Single(map.Walls);
        Assert.Single(map.Regions);
        Assert.Equal(RegionType.DifficultTerrain, map.Regions[0].RegionType);
        Assert.Equal(4, map.Regions[0].Points.Count);
    }

    [Fact]
    public void ListScenes_OrdersByFolderHierarchy()
    {
        using var temp = new TempDir();
        var data = Path.Combine(temp.Path, "Data");
        var worldData = Path.Combine(data, "worlds", "folder-world", "data");
        var scenesDb = Path.Combine(worldData, "scenes");
        var foldersDb = Path.Combine(worldData, "folders");
        Directory.CreateDirectory(scenesDb);
        Directory.CreateDirectory(foldersDb);
        File.WriteAllText(
            Path.Combine(data, "worlds", "folder-world", "world.json"),
            """{"id":"folder-world","title":"Folder World"}""");

        using (var folders = new DB(foldersDb, new Options { CreateIfMissing = true }))
        {
            folders.Put(
                "!folders!rootFolder",
                """{"_id":"rootFolder","name":"Absalom","type":"Scene","folder":null,"sort":0}""");
            folders.Put(
                "!folders!childFolder",
                """{"_id":"childFolder","name":"Streets","type":"Scene","folder":"rootFolder","sort":0}""");
            folders.Put(
                "!folders!actorFolder",
                """{"_id":"actorFolder","name":"NPCs","type":"Actor","folder":null,"sort":0}""");
        }

        using (var scenes = new DB(scenesDb, new Options { CreateIfMissing = true }))
        {
            scenes.Put(
                "!scenes!rootScene",
                """{"_id":"rootScene","name":"Wilderness","width":100,"height":100,"grid":{"size":100},"folder":null,"sort":0,"walls":[],"regions":[],"levels":[]}""");
            scenes.Put(
                "!scenes!nestedScene",
                """{"_id":"nestedScene","name":"Alley","width":100,"height":100,"grid":{"size":100},"folder":"childFolder","sort":10,"walls":[],"regions":[],"levels":[]}""");
            scenes.Put(
                "!scenes!cityScene",
                """{"_id":"cityScene","name":"Plaza","width":100,"height":100,"grid":{"size":100},"folder":"rootFolder","sort":5,"walls":[],"regions":[],"levels":[]}""");
        }

        var catalog = new FoundryWorldCatalog();
        var summaries = catalog.ListScenes(data, "folder-world");

        Assert.Equal(
            ["Wilderness", "Plaza", "Alley"],
            summaries.Select(scene => scene.Name).ToArray());
        Assert.Equal(string.Empty, summaries[0].FolderPath);
        Assert.Equal("Absalom", summaries[1].FolderPath);
        Assert.Equal("Absalom / Streets", summaries[2].FolderPath);
    }

    private static void SeedLevelDb(string directory)
    {
        using var db = new DB(directory, new Options { CreateIfMissing = true });
        db.Put(
            "!scenes!sc01",
            """{"_id":"sc01","name":"Seed Scene","width":400,"height":300,"grid":{"size":100},"initialLevel":"defaultLevel0000","walls":["w1","w2"],"regions":["r1"],"levels":["defaultLevel0000","otherLevel"]}""");
        db.Put(
            "!scenes.walls!sc01.w1",
            """{"_id":"w1","c":[10,20,30,40],"sight":20,"light":20,"sound":20,"move":20,"door":0,"levels":["defaultLevel0000"]}""");
        db.Put(
            "!scenes.walls!sc01.w2",
            """{"_id":"w2","c":[50,60,70,80],"sight":20,"light":20,"sound":20,"move":20,"door":0,"levels":["otherLevel"]}""");
        db.Put(
            "!scenes.regions!sc01.r1",
            """{"_id":"r1","name":"Mud","color":"#cc7428","shapes":[{"type":"rectangle","x":0,"y":0,"width":50,"height":50,"hole":false}],"behaviors":["b1"],"levels":[]}""");
        db.Put(
            "!scenes.regions.behaviors!sc01.r1.b1",
            """{"_id":"b1","name":"Difficult Terrain","type":"modifyMovementCost","system":{"difficulties":{"walk":2}},"disabled":false}""");
        db.Put(
            "!scenes.levels!sc01.defaultLevel0000",
            """{"_id":"defaultLevel0000","name":"Level","background":{"src":null}}""");
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "brickwork-world-catalog-" + Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
                // best-effort
            }
        }
    }
}
