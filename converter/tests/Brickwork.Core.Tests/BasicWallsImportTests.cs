using Brickwork.Core.Models;
using Brickwork.Inkarnate;
using Brickwork.Inkarnate.Parsing;
using System.Text.Json;
using Xunit;

namespace Brickwork.Core.Tests;

public class BasicWallsImportTests
{
    private static string BasicWallsInkPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "test-maps", "basic-walls.ink"));

    [Fact]
    public async Task ImportAsync_LoadsFiveWalls_FromBasicWallsInk()
    {
        Assert.True(File.Exists(BasicWallsInkPath), $"Missing test resource: {BasicWallsInkPath}");

        var importer = new InkarnateImporter();
        await using var input = File.OpenRead(BasicWallsInkPath);

        var map = await importer.ImportAsync(input);

        Assert.Equal(5, map.Walls.Count);

        var names = map.Walls.Select(wall => wall.Name).OrderBy(name => name).ToList();
        Assert.Equal(
            ["bezier-wall", "closed-path-wall", "freehand-wall", "gapped-wall", "straight-wall"],
            names);

        foreach (var wall in map.Walls)
        {
            Assert.True(wall.Points.Count >= 2, $"Wall {wall.EntityId} has too few points.");
            Assert.Equal("layer-object-72", wall.LayerId);
            Assert.True(wall.WallEnabled);
            Assert.True(wall.IsActive);
            Assert.False(string.IsNullOrWhiteSpace(wall.PathData));
            Assert.Equal(100, wall.WallThickness, precision: 1);
        }

        var straightWall = map.Walls.Single(wall => wall.EntityId == 3);
        Assert.Equal(2, straightWall.Points.Count);

        var gappedWall = map.Walls.Single(wall => wall.EntityId == 5);
        Assert.Equal(2, gappedWall.Points.Count);
        Assert.Single(gappedWall.Portals);
        Assert.Equal(1012.49, gappedWall.Portals[0].Width, precision: 1);

        var closedWall = map.Walls.Single(wall => wall.EntityId == 6);
        Assert.Equal(4, closedWall.Points.Count);
        Assert.True(closedWall.IsClosed);

        var transform = SceneTransform.FromMap(map);
        Assert.NotNull(transform);

        var previewPoint = transform!.SceneToPreview(map.Walls[0].Points[0]);
        Assert.InRange(previewPoint.X, 0, map.Preview!.Width);
        Assert.InRange(previewPoint.Y, 0, map.Preview.Height);
    }

    [Fact]
    public async Task ImportAsync_BezierWall_IsSimplifiedAtDefaultTolerance()
    {
        await using var input = File.OpenRead(BasicWallsInkPath);
        var map = await new InkarnateImporter().ImportAsync(input);

        var bezierWall = map.Walls.Single(wall => wall.EntityId == 4);

        Assert.InRange(bezierWall.Points.Count, 2, 20);
        Assert.True(
            bezierWall.RawPoints.Count > bezierWall.Points.Count,
            $"Expected simplification to reduce bezier-wall nodes (raw={bezierWall.RawPoints.Count}, simplified={bezierWall.Points.Count}).");
    }
}

public class PathV2ParsingTests
{
    [Fact]
    public void ParseToScenePoints_ReadsStraightLine()
    {
        var points = InkSvgPathParser.ParseToScenePoints(
            "M100,100l200,0",
            originX: 0,
            originY: 0,
            scale: 1);

        Assert.Equal(2, points.Count);
        Assert.Equal(100, points[0].X, precision: 1);
        Assert.Equal(100, points[0].Y, precision: 1);
        Assert.Equal(300, points[^1].X, precision: 1);
        Assert.Equal(100, points[^1].Y, precision: 1);
    }

    [Fact]
    public void ParseToScenePoints_ReadsClosedRectangle()
    {
        var points = InkSvgPathParser.ParseToScenePoints(
            "M-410.52,331.96v-663.92h821.04v663.92z",
            originX: 759,
            originY: 5421,
            scale: 1);

        Assert.Equal(4, points.Count);
    }

    [Fact]
    public void ParseToScenePoints_SamplesBezierCurves()
    {
        var points = InkSvgPathParser.ParseToScenePoints(
            "M0,0c50,50 100,0 150,50",
            originX: 0,
            originY: 0,
            scale: 1);

        Assert.True(points.Count > 2);
        Assert.True(points.Count < 32);
    }

    [Fact]
    public void ParseToScenePoints_AppliesTransform()
    {
        var points = InkSvgPathParser.ParseToScenePoints(
            "M10,10l10,0",
            originX: 100,
            originY: 200,
            scale: 2);

        Assert.Equal(2, points.Count);
        Assert.Equal(120, points[0].X, precision: 1);
        Assert.Equal(220, points[0].Y, precision: 1);
        Assert.Equal(140, points[^1].X, precision: 1);
        Assert.Equal(220, points[^1].Y, precision: 1);
    }
}

public class EntityUpdateHandlerTests
{
    [Fact]
    public void Process_AppliesRenameAndPortals()
    {
        const string json = """
            {
              "title": "updates",
              "version": 3,
              "history": [
                {
                  "transactionId": 1,
                  "cmdType": "cmd-entity-add",
                  "items": [{
                    "layerId": "layer-object-72",
                    "entity": {
                      "entityType": "path-v2",
                      "entityId": 5,
                      "defaultName": "Wall",
                      "wallEnabled": true,
                      "paths": "M0,0l100,0",
                      "x": 0,
                      "y": 0,
                      "scale": 1
                    }
                  }]
                },
                {
                  "transactionId": 2,
                  "cmdType": "cmd-entity-update",
                  "items": [{
                    "entityId": 5,
                    "update": {
                      "name": "gapped-wall",
                      "portals": [{
                        "id": "portal-1",
                        "width": 1012.49,
                        "anchor": { "x": -48.77, "y": 14.19 }
                      }]
                    }
                  }]
                }
              ]
            }
            """;

        using var document = JsonDocument.Parse(json);
        var map = InkarnateDocumentParser.Parse(document.RootElement);

        var wall = Assert.Single(map.Walls);
        Assert.Equal("gapped-wall", wall.Name);
        Assert.Single(wall.Portals);
        Assert.Equal(1012.49, wall.Portals[0].Width, precision: 2);
    }

    [Fact]
    public void Process_AppliesIsVisible_AndPreservesAcrossLayerShow()
    {
        const string json = """
            {
              "title": "visibility",
              "version": 3,
              "history": [
                {
                  "transactionId": 1,
                  "cmdType": "cmd-layer-add",
                  "layerId": "layer-a",
                  "layerKind": "entity",
                  "layerData": { "name": "A", "isVisible": true }
                },
                {
                  "transactionId": 2,
                  "cmdType": "cmd-entity-add",
                  "items": [{
                    "layerId": "layer-a",
                    "entity": {
                      "entityType": "path-v2",
                      "entityId": 29695,
                      "wallEnabled": true,
                      "paths": "M0,0l100,0",
                      "x": 0,
                      "y": 0,
                      "scale": 1
                    }
                  }]
                },
                {
                  "transactionId": 3,
                  "cmdType": "cmd-entity-update",
                  "items": [{ "entityId": 29695, "update": { "isVisible": false } }]
                },
                {
                  "transactionId": 4,
                  "cmdType": "cmd-layer-update-visibility",
                  "layerId": "layer-a",
                  "isVisible": false
                },
                {
                  "transactionId": 5,
                  "cmdType": "cmd-layer-update-visibility",
                  "layerId": "layer-a",
                  "isVisible": true
                }
              ]
            }
            """;

        using var document = JsonDocument.Parse(json);
        var map = InkarnateDocumentParser.Parse(document.RootElement);

        var wall = Assert.Single(map.Walls);
        Assert.False(wall.IsEntityVisible);
        Assert.False(wall.IsActive);
        Assert.Equal(0, map.Compatibility!.UnknownCount);
    }

    [Fact]
    public void Process_AppliesWallEnabled()
    {
        const string json = """
            {
              "title": "wall-enabled",
              "version": 3,
              "history": [
                {
                  "transactionId": 1,
                  "cmdType": "cmd-entity-add",
                  "items": [{
                    "layerId": "layer-a",
                    "entity": {
                      "entityType": "path-v2",
                      "entityId": 7,
                      "wallEnabled": true,
                      "paths": "M0,0l100,0",
                      "x": 0,
                      "y": 0,
                      "scale": 1
                    }
                  }]
                },
                {
                  "transactionId": 2,
                  "cmdType": "cmd-entity-update",
                  "items": [{ "entityId": 7, "update": { "wallEnabled": false } }]
                }
              ]
            }
            """;

        using var document = JsonDocument.Parse(json);
        var map = InkarnateDocumentParser.Parse(document.RootElement);

        var wall = Assert.Single(map.Walls);
        Assert.False(wall.WallEnabled);
    }
}

public class MapExportFiltersTests
{
    [Fact]
    public void ExportableWalls_ExcludesInactiveWalls()
    {
        var map = new MapDocument
        {
            Walls =
            [
                new Wall { EntityId = 1, WallEnabled = true, IsActive = true },
                new Wall { EntityId = 2, WallEnabled = true, IsActive = false },
                new Wall { EntityId = 3, WallEnabled = false, IsActive = true },
            ],
        };

        var exportable = map.ExportableWalls().ToList();
        Assert.Single(exportable);
        Assert.Equal(1, exportable[0].EntityId);
    }
}
