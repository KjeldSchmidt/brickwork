using Brickwork.Core.Geometry;
using Brickwork.Core.Models;
using Brickwork.Inkarnate;
using Brickwork.Inkarnate.Parsing;
using Xunit;

namespace Brickwork.Core.Tests;

public class WallsWithHistoryImportTests
{
    private static string WallsWithHistoryInkPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "test-maps", "walls-with-history.ink"));

    [Fact]
    public async Task ImportAsync_RemovesDeletedWall()
    {
        var map = await LoadAsync();

        Assert.DoesNotContain(map.Walls, wall => wall.EntityId == 2);
        Assert.DoesNotContain(map.Walls, wall => wall.EntityId == 5);
        Assert.DoesNotContain(map.Walls, wall => wall.EntityId == 10);
        Assert.Equal([3, 4, 6], map.Walls.Select(wall => wall.EntityId).OrderBy(id => id).ToList());
    }

    [Fact]
    public async Task ImportAsync_TracksNestedGroupMembership()
    {
        var map = await LoadAsync();

        Assert.Equal([7, 8, 9], map.Groups.Select(group => group.GroupId).OrderBy(id => id).ToList());
        Assert.DoesNotContain(map.Groups, group => group.GroupId == 11);

        var group7 = map.Groups.Single(group => group.GroupId == 7);
        Assert.Equal(8, group7.ParentGroupId);
        Assert.Equal([4], group7.MemberIds.ToList());

        var group8 = map.Groups.Single(group => group.GroupId == 8);
        Assert.Null(group8.ParentGroupId);
        Assert.Contains(7, group8.MemberIds);
        Assert.Contains(3, group8.MemberIds);

        var group9 = map.Groups.Single(group => group.GroupId == 9);
        Assert.Empty(group9.MemberIds);

        Assert.Equal(7, map.Walls.Single(wall => wall.EntityId == 4).GroupId);
        Assert.Equal(8, map.Walls.Single(wall => wall.EntityId == 3).GroupId);
        Assert.Null(map.Walls.Single(wall => wall.EntityId == 6).GroupId);
    }

    [Fact]
    public async Task ImportAsync_AppliesRotationWithNewOrigin()
    {
        var map = await LoadAsync();
        var bezier = map.Walls.Single(wall => wall.EntityId == 4);

        Assert.Equal(300.7, bezier.Angle, precision: 3);
        Assert.Equal(6368, bezier.Origin.X, precision: 0);
        Assert.Equal(2160, bezier.Origin.Y, precision: 0);
        Assert.Equal(bezier.Origin, bezier.PathOrigin);
        Assert.Equal(5448, bezier.RotationPivot.X, precision: 0);
        Assert.Equal(1422, bezier.RotationPivot.Y, precision: 0);

        // R(local) + new (x,y): first local ~ (676, -628) → ~ (6173, 1258).
        var first = bezier.Points[0];
        Assert.InRange(first.X, 6100, 6250);
        Assert.InRange(first.Y, 1200, 1320);
    }

    [Fact]
    public void ImportAsync_AppliesTranslation()
    {
        const string json = """
            {
              "title": "translate",
              "version": 3,
              "history": [
                {
                  "transactionId": 1,
                  "cmdType": "cmd-entity-add",
                  "items": [{
                    "layerId": "layer-object-72",
                    "entity": {
                      "entityType": "path-v2",
                      "entityId": 1,
                      "wallEnabled": true,
                      "wallThickness": 10,
                      "x": 100,
                      "y": 200,
                      "scale": 1,
                      "paths": "M0,0 L10,0"
                    }
                  }]
                },
                {
                  "transactionId": 2,
                  "cmdType": "cmd-entity-update",
                  "items": [{ "entityId": 1, "update": { "x": 150, "y": 260 } }]
                }
              ]
            }
            """;

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var map = InkarnateDocumentParser.Parse(document.RootElement);
        var wall = Assert.Single(map.Walls);

        Assert.Equal(150, wall.Origin.X, precision: 0);
        Assert.Equal(260, wall.Origin.Y, precision: 0);
        Assert.Equal(wall.Origin, wall.PathOrigin);
        Assert.Equal(150, wall.Points[0].X, precision: 0);
        Assert.Equal(260, wall.Points[0].Y, precision: 0);
    }

    [Fact]
    public async Task ImportAsync_CompositeAndRemoveAreUnderstood()
    {
        var map = await LoadAsync();
        Assert.NotNull(map.Compatibility);
        Assert.Equal(0, map.Compatibility!.UnknownCount);

        var commandTypes = map.Compatibility.Transactions.Select(tx => tx.CommandType).ToHashSet();
        Assert.Contains("cmd-composite", commandTypes);
        Assert.Contains("cmd-entity-remove", commandTypes);
    }

    [Fact]
    public void RotateAround_UsesPivot()
    {
        var rotated = MapPointTransforms.RotateAround(new MapPoint(10, 0), new MapPoint(0, 0), 90);
        Assert.Equal(0, rotated.X, precision: 6);
        Assert.Equal(10, rotated.Y, precision: 6);
    }

    [Fact]
    public void Import_GroupRotation_RotatesMemberGeometryAroundPivot()
    {
        const string json = """
            {
              "title": "group-rotate",
              "version": 3,
              "scene": { "normSceneSize": { "w": 1000, "h": 1000 } },
              "preview": { "width": 100, "height": 100 },
              "history": [
                {
                  "transactionId": 1,
                  "cmdType": "cmd-entity-add",
                  "items": [{
                    "layerId": "layer-object-72",
                    "entity": {
                      "entityType": "path-v2",
                      "entityId": 1,
                      "defaultName": "wall",
                      "wallEnabled": true,
                      "wallThickness": 10,
                      "x": 10,
                      "y": 0,
                      "scale": 1,
                      "paths": "M0,0 L10,0"
                    }
                  }]
                },
                {
                  "transactionId": 2,
                  "cmdType": "cmd-composite",
                  "cmds": [
                    { "cmdType": "cmd-entity-group", "groupId": 2, "entityIds": [1] },
                    {
                      "cmdType": "cmd-entity-update",
                      "items": [{ "entityId": 2, "update": { "name": "G" } }]
                    }
                  ]
                },
                {
                  "transactionId": 3,
                  "cmdType": "cmd-entity-update",
                  "items": [{
                    "entityId": 2,
                    "update": { "angle": 90, "oX": 0, "oY": 0, "x": 0, "y": 0 }
                  }]
                }
              ]
            }
            """;

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var map = InkarnateDocumentParser.Parse(document.RootElement);

        var wall = Assert.Single(map.Walls);
        Assert.Equal(0, wall.Points[0].X, precision: 6);
        Assert.Equal(10, wall.Points[0].Y, precision: 6);
        Assert.Equal(90, map.Groups.Single().Angle, precision: 6);
    }

    [Fact]
    public void Import_NestedGroupRotation_RotatesInnerWallAroundOuterPivot()
    {
        const string json = """
            {
              "title": "nested-group-rotate",
              "version": 3,
              "scene": { "normSceneSize": { "w": 1000, "h": 1000 } },
              "preview": { "width": 100, "height": 100 },
              "history": [
                {
                  "transactionId": 1,
                  "cmdType": "cmd-entity-add",
                  "items": [{
                    "layerId": "layer-object-72",
                    "entity": {
                      "entityType": "path-v2",
                      "entityId": 1,
                      "defaultName": "wall",
                      "wallEnabled": true,
                      "wallThickness": 10,
                      "x": 10,
                      "y": 0,
                      "scale": 1,
                      "paths": "M0,0 L10,0"
                    }
                  }]
                },
                {
                  "transactionId": 2,
                  "cmdType": "cmd-composite",
                  "cmds": [
                    { "cmdType": "cmd-entity-group", "groupId": 2, "entityIds": [1] },
                    { "cmdType": "cmd-entity-update", "items": [{ "entityId": 2, "update": { "name": "Inner" } }] }
                  ]
                },
                {
                  "transactionId": 3,
                  "cmdType": "cmd-composite",
                  "cmds": [
                    { "cmdType": "cmd-entity-group", "groupId": 3, "entityIds": [2] },
                    { "cmdType": "cmd-entity-update", "items": [{ "entityId": 3, "update": { "name": "Outer" } }] }
                  ]
                },
                {
                  "transactionId": 4,
                  "cmdType": "cmd-entity-update",
                  "items": [{
                    "entityId": 3,
                    "update": { "angle": 90, "oX": 0, "oY": 0, "x": 0, "y": 0 }
                  }]
                }
              ]
            }
            """;

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var map = InkarnateDocumentParser.Parse(document.RootElement);

        var wall = Assert.Single(map.Walls);
        Assert.Equal(0, wall.Points[0].X, precision: 6);
        Assert.Equal(10, wall.Points[0].Y, precision: 6);

        var outer = map.Groups.Single(group => group.GroupId == 3);
        var inner = map.Groups.Single(group => group.GroupId == 2);
        Assert.Equal(90, outer.Angle, precision: 6);
        Assert.Equal(3, inner.ParentGroupId);
        Assert.Equal(2, wall.GroupId);
    }

    [Fact]
    public void Import_Ungroup_ClearsMembership()
    {
        const string json = """
            {
              "title": "ungroup",
              "version": 3,
              "history": [
                {
                  "transactionId": 1,
                  "cmdType": "cmd-entity-add",
                  "items": [{
                    "layerId": "layer-object-72",
                    "entity": {
                      "entityType": "path-v2",
                      "entityId": 1,
                      "wallEnabled": true,
                      "wallThickness": 10,
                      "x": 0,
                      "y": 0,
                      "scale": 1,
                      "paths": "M0,0 L10,0"
                    }
                  }]
                },
                {
                  "transactionId": 2,
                  "cmdType": "cmd-composite",
                  "cmds": [
                    { "cmdType": "cmd-entity-group", "groupId": 2, "entityIds": [1] },
                    { "cmdType": "cmd-entity-update", "items": [{ "entityId": 2, "update": { "name": "G" } }] }
                  ]
                },
                {
                  "transactionId": 3,
                  "cmdType": "cmd-composite",
                  "cmds": [
                    { "cmdType": "cmd-entity-move-to-group", "entityIds": [1] },
                    { "cmdType": "cmd-entity-update", "items": [{ "entityId": 1, "update": { "order": 1 } }] }
                  ]
                }
              ]
            }
            """;

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var map = InkarnateDocumentParser.Parse(document.RootElement);

        Assert.Null(Assert.Single(map.Walls).GroupId);
        Assert.Empty(map.Groups.Single(group => group.GroupId == 2).MemberIds);
    }

    [Fact]
    public void Import_DeleteGroup_DeletesMembers()
    {
        const string json = """
            {
              "title": "delete-group",
              "version": 3,
              "history": [
                {
                  "transactionId": 1,
                  "cmdType": "cmd-entity-add",
                  "items": [
                    {
                      "layerId": "layer-object-72",
                      "entity": {
                        "entityType": "path-v2",
                        "entityId": 1,
                        "wallEnabled": true,
                        "wallThickness": 10,
                        "x": 0,
                        "y": 0,
                        "scale": 1,
                        "paths": "M0,0 L10,0"
                      }
                    },
                    {
                      "layerId": "layer-object-72",
                      "entity": {
                        "entityType": "path-v2",
                        "entityId": 2,
                        "wallEnabled": true,
                        "wallThickness": 10,
                        "x": 20,
                        "y": 0,
                        "scale": 1,
                        "paths": "M0,0 L10,0"
                      }
                    }
                  ]
                },
                {
                  "transactionId": 2,
                  "cmdType": "cmd-composite",
                  "cmds": [
                    { "cmdType": "cmd-entity-group", "groupId": 3, "entityIds": [1, 2] },
                    { "cmdType": "cmd-entity-update", "items": [{ "entityId": 3, "update": { "name": "G" } }] }
                  ]
                },
                {
                  "transactionId": 3,
                  "cmdType": "cmd-entity-remove",
                  "entityIds": [3]
                }
              ]
            }
            """;

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var map = InkarnateDocumentParser.Parse(document.RootElement);

        Assert.Empty(map.Walls);
        Assert.DoesNotContain(map.Groups, group => group.GroupId == 3);
    }

    [Fact]
    public void Import_NestGroupInGroup_SetsParentLinks()
    {
        const string json = """
            {
              "title": "nest",
              "version": 3,
              "history": [
                {
                  "transactionId": 1,
                  "cmdType": "cmd-entity-add",
                  "items": [{
                    "layerId": "layer-object-72",
                    "entity": {
                      "entityType": "path-v2",
                      "entityId": 1,
                      "wallEnabled": true,
                      "wallThickness": 10,
                      "x": 0,
                      "y": 0,
                      "scale": 1,
                      "paths": "M0,0 L10,0"
                    }
                  }]
                },
                {
                  "transactionId": 2,
                  "cmdType": "cmd-composite",
                  "cmds": [
                    { "cmdType": "cmd-entity-group", "groupId": 2, "entityIds": [1] },
                    { "cmdType": "cmd-entity-update", "items": [{ "entityId": 2, "update": { "name": "Inner" } }] }
                  ]
                },
                {
                  "transactionId": 3,
                  "cmdType": "cmd-composite",
                  "cmds": [
                    { "cmdType": "cmd-entity-group", "groupId": 3, "entityIds": [2] },
                    { "cmdType": "cmd-entity-update", "items": [{ "entityId": 3, "update": { "name": "Outer" } }] }
                  ]
                }
              ]
            }
            """;

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var map = InkarnateDocumentParser.Parse(document.RootElement);

        var inner = map.Groups.Single(group => group.GroupId == 2);
        var outer = map.Groups.Single(group => group.GroupId == 3);
        Assert.Equal(3, inner.ParentGroupId);
        Assert.Equal([2], outer.MemberIds.ToList());
        Assert.Equal([1], inner.MemberIds.ToList());
        Assert.Equal(2, map.Walls.Single().GroupId);
    }

    private static async Task<MapDocument> LoadAsync()
    {
        Assert.True(File.Exists(WallsWithHistoryInkPath), $"Missing test resource: {WallsWithHistoryInkPath}");
        await using var input = File.OpenRead(WallsWithHistoryInkPath);
        return await new InkarnateImporter().ImportAsync(input);
    }
}
