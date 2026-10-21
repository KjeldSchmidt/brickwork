using System.Text.Json;
using Brickwork.Core.Models;
using Brickwork.Core.Settings;
using Brickwork.Inkarnate.Parsing;
using Xunit;

namespace Brickwork.Core.Tests;

public class PathTemplateTests
{
    [Fact]
    public void Import_ReadsPathId_AndFollowsUpdates()
    {
        const string json = """
            {
              "title": "path styles",
              "version": 3,
              "history": [
                {
                  "transactionId": 1,
                  "cmdType": "cmd-entity-add",
                  "items": [
                    { "layerId": "layer-a", "entity": { "entityType": "path-v2", "entityId": 5, "defaultName": "Wall",
                      "wallEnabled": true, "paths": "M0,0l100,0", "x": 0, "y": 0, "scale": 1, "pathId": 6552105 } },
                    { "layerId": "layer-a", "entity": { "entityType": "path-v2", "entityId": 6, "defaultName": "Wall",
                      "wallEnabled": true, "paths": "M0,0l0,100", "x": 0, "y": 0, "scale": 1, "pathId": 6552105 } }
                  ]
                },
                {
                  "transactionId": 2,
                  "cmdType": "cmd-entity-update",
                  "items": [{ "entityId": 6, "update": { "pathId": 3232381 } }]
                }
              ]
            }
            """;

        using var document = JsonDocument.Parse(json);
        var map = InkarnateDocumentParser.Parse(document.RootElement);

        var walls = map.Walls.ToDictionary(wall => wall.EntityId);
        Assert.Equal(6552105, walls[5].PathTemplateId);
        Assert.Equal(3232381, walls[6].PathTemplateId);
    }

    [Fact]
    public void DisplayName_UsesStyleNameUnlessWallHasItsOwn()
    {
        var library = new PathTemplateLibrary(user: new Dictionary<int, string> { [1] = "Rope Fence" });

        Assert.Equal("Rope Fence", library.DisplayName(new Wall { Name = "Wall", PathTemplateId = 1 }));
        Assert.Equal("Rope Fence", library.DisplayName(new Wall { PathTemplateId = 1 }));
        Assert.Equal("Rope Fence (hole 1)", library.DisplayName(new Wall { Name = "Wall (hole 1)", PathTemplateId = 1 }));
        Assert.Equal("Gate", library.DisplayName(new Wall { Name = "Gate", PathTemplateId = 1 }));
        Assert.Equal("Path 2", library.DisplayName(new Wall { Name = "Wall", PathTemplateId = 2 }));
        Assert.Equal("Wall", library.DisplayName(new Wall { Name = "Wall" }));
    }

    [Fact]
    public void SetName_OverridesBuiltIn_AndBlankRestoresIt()
    {
        var library = new PathTemplateLibrary(builtIn: new Dictionary<int, string> { [1] = "Rope Fence" });

        library.SetName(1, "Fence");
        Assert.Equal("Fence", library.GetName(1));
        Assert.Equal("Fence", library.UserValues[1]);

        library.SetName(1, "  ");
        Assert.Equal("Rope Fence", library.GetName(1));
        Assert.Empty(library.UserValues);

        // Naming it like the built-in keeps nothing in the user's file.
        library.SetName(1, "Rope Fence");
        Assert.Empty(library.UserValues);
    }

    [Fact]
    public void Store_RoundTripsNames()
    {
        var directory = Path.Combine(Path.GetTempPath(), "brickwork-path-templates-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new PathTemplateLibraryStore(directory);

            store.Save(new Dictionary<int, string> { [6552105] = "Rope Fence", [3232381] = "Wood Roof" });

            var loaded = store.Load();
            Assert.Equal("Rope Fence", loaded[6552105]);
            Assert.Equal("Wood Roof", loaded[3232381]);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void BuiltInLibrary_Loads()
    {
        // Ships empty for now; must still be readable.
        Assert.NotNull(PathTemplateLibraryStore.LoadBuiltIn());
    }
}
