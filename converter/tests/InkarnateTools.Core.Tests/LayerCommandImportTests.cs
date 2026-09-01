using InkarnateTools.Core.Models;
using InkarnateTools.Inkarnate.Parsing;
using Xunit;

namespace InkarnateTools.Core.Tests;

public class LayerCommandImportTests
{
    [Fact]
    public void Import_TracksLayerNameOrderVisibilityAndMove()
    {
        const string json = """
            {
              "title": "layers",
              "version": 3,
              "history": [
                {
                  "transactionId": 1,
                  "cmdType": "cmd-layer-add",
                  "layerId": "layer-a",
                  "layerKind": "entity",
                  "layerData": { "name": "Alpha", "isVisible": true }
                },
                {
                  "transactionId": 2,
                  "cmdType": "cmd-layer-add",
                  "layerId": "layer-b",
                  "layerKind": "entity",
                  "layerData": { "name": "Beta", "isVisible": true }
                },
                {
                  "transactionId": 3,
                  "cmdType": "cmd-layer-add",
                  "layerId": "layer-c",
                  "layerKind": "entity",
                  "layerData": { "name": "Gamma", "isVisible": false }
                },
                {
                  "transactionId": 4,
                  "cmdType": "cmd-entity-add",
                  "items": [
                    {
                      "layerId": "layer-a",
                      "entity": {
                        "entityType": "path-v2",
                        "entityId": 1,
                        "wallEnabled": true,
                        "x": 0,
                        "y": 0,
                        "scale": 1,
                        "paths": "M0,0 L10,0"
                      }
                    },
                    {
                      "layerId": "layer-c",
                      "entity": {
                        "entityType": "path-v2",
                        "entityId": 2,
                        "wallEnabled": true,
                        "x": 0,
                        "y": 0,
                        "scale": 1,
                        "paths": "M0,0 L10,0"
                      }
                    }
                  ]
                },
                {
                  "transactionId": 5,
                  "cmdType": "cmd-layer-update-name",
                  "layerId": "layer-b",
                  "name": "Floor"
                },
                {
                  "transactionId": 6,
                  "cmdType": "cmd-layer-reorder",
                  "newLayerOrder": [ "layer-c", "layer-b", "layer-a" ]
                },
                {
                  "transactionId": 7,
                  "cmdType": "cmd-entity-move-to-layer",
                  "targetLayerId": "layer-b",
                  "entityIds": [ 1 ]
                },
                {
                  "transactionId": 8,
                  "cmdType": "cmd-layer-update-visibility",
                  "layerId": "layer-b",
                  "isVisible": false
                },
                {
                  "transactionId": 9,
                  "cmdType": "cmd-layer-update-layer-shadows",
                  "layerId": "layer-a"
                },
                {
                  "transactionId": 10,
                  "cmdType": "cmd-metadata",
                  "key": "x"
                }
              ]
            }
            """;

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var map = InkarnateDocumentParser.Parse(document.RootElement);

        Assert.Equal(["layer-c", "layer-b", "layer-a"], map.Layers.Select(layer => layer.Id).ToList());
        Assert.Equal("Floor", map.Layers.Single(layer => layer.Id == "layer-b").Name);
        Assert.False(map.Layers.Single(layer => layer.Id == "layer-b").IsVisible);
        Assert.False(map.Layers.Single(layer => layer.Id == "layer-c").IsVisible);

        var wall1 = map.Walls.Single(wall => wall.EntityId == 1);
        var wall2 = map.Walls.Single(wall => wall.EntityId == 2);
        Assert.Equal("layer-b", wall1.LayerId);
        Assert.False(wall1.IsActive);
        Assert.Equal("layer-c", wall2.LayerId);
        Assert.False(wall2.IsActive);

        Assert.NotNull(map.Compatibility);
        Assert.Equal(0, map.Compatibility!.UnknownCount);
        Assert.Equal(2, map.Compatibility.KnownIgnoredCount);
        Assert.Contains(
            map.Compatibility.Transactions,
            tx => tx.CommandType == "cmd-layer-update-layer-shadows" &&
                  tx.Understanding == TransactionUnderstanding.KnownIgnored);
        Assert.Contains(
            map.Compatibility.Transactions,
            tx => tx.CommandType == "cmd-metadata" &&
                  tx.Understanding == TransactionUnderstanding.KnownIgnored);
    }

    [Fact]
    public void Import_MoveToLayer_UpdatesGroupedWalls()
    {
        const string json = """
            {
              "title": "move group",
              "version": 3,
              "history": [
                {
                  "transactionId": 1,
                  "cmdType": "cmd-layer-add",
                  "layerId": "layer-a",
                  "layerData": { "name": "A", "isVisible": true }
                },
                {
                  "transactionId": 2,
                  "cmdType": "cmd-layer-add",
                  "layerId": "layer-b",
                  "layerData": { "name": "B", "isVisible": true }
                },
                {
                  "transactionId": 3,
                  "cmdType": "cmd-entity-add",
                  "items": [{
                    "layerId": "layer-a",
                    "entity": {
                      "entityType": "path-v2",
                      "entityId": 1,
                      "wallEnabled": true,
                      "x": 0,
                      "y": 0,
                      "scale": 1,
                      "paths": "M0,0 L10,0"
                    }
                  }]
                },
                {
                  "transactionId": 4,
                  "cmdType": "cmd-composite",
                  "cmds": [
                    {
                      "cmdType": "cmd-entity-group",
                      "groupId": 10,
                      "entityIds": [ 1 ]
                    }
                  ]
                },
                {
                  "transactionId": 5,
                  "cmdType": "cmd-entity-move-to-layer",
                  "targetLayerId": "layer-b",
                  "entityIds": [ 10 ]
                }
              ]
            }
            """;

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var map = InkarnateDocumentParser.Parse(document.RootElement);

        Assert.Equal("layer-b", map.Walls.Single().LayerId);
        Assert.Equal("layer-b", map.Groups.Single().LayerId);
    }

    [Fact]
    public void Import_Composite_ShowsNestedCommandsInDebugOverview()
    {
        const string json = """
            {
              "title": "composite",
              "version": 3,
              "history": [
                {
                  "transactionId": 6,
                  "cmdType": "cmd-composite",
                  "cmds": [
                    {
                      "cmdType": "cmd-layer-add",
                      "layerId": "layer-grid",
                      "layerData": {
                        "isVisible": false,
                        "name": "Grid",
                        "opacity": 1
                      },
                      "layerKind": "entity",
                      "atIndex": 0
                    },
                    {
                      "cmdType": "cmd-entity-add",
                      "items": [
                        {
                          "layerId": "layer-grid",
                          "entity": {
                            "entityId": 1,
                            "entityType": "grid",
                            "style": { "size": 100 }
                          }
                        }
                      ]
                    }
                  ]
                },
                { "transactionId": 7, "cmdType": "cmd-mask" },
                { "transactionId": 8, "cmdType": "cmd-set-base-color" }
              ]
            }
            """;

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var map = InkarnateDocumentParser.Parse(document.RootElement);

        Assert.Equal(100, map.Grid.CellSize);
        Assert.Equal("Grid", Assert.Single(map.Layers).Name);
        Assert.False(map.Layers[0].IsVisible);

        Assert.NotNull(map.Compatibility);
        var composite = Assert.Single(
            map.Compatibility!.Transactions,
            tx => tx.CommandType == "cmd-composite");
        Assert.Equal(TransactionUnderstanding.FullyUnderstood, composite.Understanding);
        Assert.Equal(2, composite.Children.Count);
        Assert.Equal("cmd-layer-add", composite.Children[0].CommandType);
        Assert.Equal("cmd-entity-add", composite.Children[1].CommandType);
        Assert.All(composite.Children, child => Assert.Equal(6, child.TransactionId));

        var details = map.Compatibility.FormatTransactionLines();
        Assert.Contains("cmd-composite", details, StringComparison.Ordinal);
        Assert.Contains("cmd-layer-add", details, StringComparison.Ordinal);
        Assert.Contains("cmd-entity-add", details, StringComparison.Ordinal);
        Assert.Equal(2, map.Compatibility.KnownIgnoredCount);
    }

    [Fact]
    public void Import_LayerRemove_RemovesLayerAndWalls()
    {
        const string json = """
            {
              "title": "layer-remove",
              "version": 3,
              "history": [
                {
                  "transactionId": 1,
                  "cmdType": "cmd-layer-add",
                  "layerId": "layer-keep",
                  "layerData": { "name": "Keep", "isVisible": true }
                },
                {
                  "transactionId": 2,
                  "cmdType": "cmd-layer-add",
                  "layerId": "layer-top",
                  "layerData": { "name": "Top", "isVisible": true }
                },
                {
                  "transactionId": 3,
                  "cmdType": "cmd-entity-add",
                  "items": [
                    {
                      "layerId": "layer-keep",
                      "entity": {
                        "entityType": "path-v2",
                        "entityId": 1,
                        "wallEnabled": true,
                        "x": 0,
                        "y": 0,
                        "scale": 1,
                        "paths": "M0,0 L10,0"
                      }
                    },
                    {
                      "layerId": "layer-top",
                      "entity": {
                        "entityType": "path-v2",
                        "entityId": 2,
                        "wallEnabled": true,
                        "x": 0,
                        "y": 0,
                        "scale": 1,
                        "paths": "M0,0 L10,0"
                      }
                    }
                  ]
                },
                {
                  "transactionId": 4,
                  "cmdType": "cmd-layer-remove",
                  "layerId": "layer-top"
                }
              ]
            }
            """;

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var map = InkarnateDocumentParser.Parse(document.RootElement);

        Assert.Equal(["layer-keep"], map.Layers.Select(layer => layer.Id).ToList());
        Assert.Equal(1, Assert.Single(map.Walls).EntityId);
        Assert.Equal(0, map.Compatibility!.UnknownCount);
    }
}
