using Brickwork.Inkarnate.Parsing;
using Xunit;

namespace Brickwork.Core.Tests;

public class GroupSubtreeRemovalTests
{
    [Fact]
    public void Import_RemovingParentGroup_RemovesNestedWalls()
    {
        const string json = """
            {
              "title": "group-cascade",
              "version": 3,
              "history": [
                {
                  "transactionId": 1,
                  "cmdType": "cmd-entity-add",
                  "items": [
                    {
                      "layerId": "layer-object-72",
                      "entity": {
                        "entityType": "group",
                        "entityId": 100,
                        "name": "Cell Unit"
                      }
                    },
                    {
                      "layerId": "layer-object-72",
                      "entity": {
                        "entityType": "group",
                        "entityId": 101,
                        "name": "Prison Cell",
                        "groupId": 100
                      }
                    },
                    {
                      "layerId": "layer-object-72",
                      "entity": {
                        "entityType": "path-v2",
                        "entityId": 1,
                        "defaultName": "Shape: Prison Bars",
                        "wallEnabled": true,
                        "wallThickness": 10,
                        "groupId": 101,
                        "x": 0,
                        "y": 0,
                        "scale": 1,
                        "paths": "M0,0 L10,0"
                      }
                    }
                  ]
                },
                {
                  "transactionId": 2,
                  "cmdType": "cmd-entity-remove",
                  "entityIds": [100]
                }
              ]
            }
            """;

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var map = InkarnateDocumentParser.Parse(document.RootElement);

        Assert.Empty(map.Walls);
        Assert.DoesNotContain(map.Groups, group => group.GroupId is 100 or 101);
    }
}
