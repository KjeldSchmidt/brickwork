using System.Text;
using System.Text.Json;
using Brickwork.Composition;
using Brickwork.Core.Editing;
using Brickwork.Core.Models;
using Brickwork.Exporters.Uvtt;
using Brickwork.Inkarnate;
using Xunit;

namespace Brickwork.Core.Tests;

public class LightImportExportTests
{
    private static readonly IReadOnlyList<string> Include = LightKeywords.Parse(LightKeywords.DefaultInclude);
    private static readonly IReadOnlyList<string> Exclude = LightKeywords.Parse(LightKeywords.DefaultExclude);

    [Theory]
    [InlineData("Hand Lantern Lit", true)]
    [InlineData("Candles", true)]
    [InlineData("Wall Torches", true)]
    [InlineData("Fireplace", true)]
    [InlineData("Oil Lantern Broken", false)]
    [InlineData("Unlit Torch", false)]
    [InlineData("Crystal Ore Container", false)]
    [InlineData("Firewood", false)]
    [InlineData("Lightning Rod", false)]
    [InlineData(null, false)]
    public void IsLight_MatchesWholeWords(string? name, bool expected)
    {
        Assert.Equal(expected, LightKeywords.IsLight(name, Include, Exclude));
    }

    [Fact]
    public async Task Import_LightStamps_FollowHistory()
    {
        var map = await ImportAsync(new InkarnateImportOptions());

        var lights = map.Lights.ToDictionary(light => light.EntityId);
        Assert.Equal([10, 13, 16], lights.Keys.Order().ToArray());

        // Moved after creation.
        Assert.Equal(new MapPoint(150, 200), lights[10].Position);
        Assert.Equal("Hand Lantern Lit", lights[10].SourceName);
        Assert.True(lights[10].IsActive);

        // On a hidden layer: imported but inactive.
        Assert.False(lights[13].IsActive);

        // Translated with its group.
        Assert.Equal(new MapPoint(50, 0), lights[16].Position);
        Assert.Equal(20, lights[16].GroupId);

        Assert.All(map.Lights, light => Assert.Equal(LightSource.DefaultDimRadius, light.DimRadius));

        // Animations come from the names.
        Assert.Equal(LightAnimation.Flicker, lights[10].Animation);
        Assert.Equal(LightAnimation.Torch, lights[13].Animation);
        Assert.Equal(LightAnimation.Torch, lights[16].Animation);
    }

    [Fact]
    public async Task Import_DetectionDisabled_CreatesNoLights()
    {
        var map = await ImportAsync(new InkarnateImportOptions { DetectLightsFromStamps = false });

        Assert.Empty(map.Lights);
    }

    [Fact]
    public async Task Import_AnimationDetectionDisabled_LeavesLightsStill()
    {
        var map = await ImportAsync(new InkarnateImportOptions { DetectLightAnimations = false });

        Assert.All(map.Lights, light => Assert.Equal(LightAnimation.None, light.Animation));
    }

    [Fact]
    public async Task Import_CustomKeywordsAndRadii_AreApplied()
    {
        var map = await ImportAsync(new InkarnateImportOptions
        {
            LightKeywords = "crate",
            LightExcludeKeywords = string.Empty,
            LightBrightRadius = 1,
            LightDimRadius = 2,
        });

        var light = Assert.Single(map.Lights);
        Assert.Equal(12, light.EntityId);
        Assert.Equal(1, light.BrightRadius);
        Assert.Equal(2, light.DimRadius);
    }

    [Fact]
    public async Task FoundryExport_WritesActiveLightsInDistanceUnits()
    {
        var map = await ImportAsync(new InkarnateImportOptions());
        await using var output = new MemoryStream();

        await ServiceFactory.CreateConvertMapService().ConvertAsync(map, output, "foundry");

        output.Position = 0;
        using var document = await JsonDocument.ParseAsync(output);
        var lights = document.RootElement.GetProperty("lights");
        Assert.Equal(2, lights.GetArrayLength());

        var first = lights[0];
        Assert.Equal(150, first.GetProperty("x").GetInt32());
        Assert.Equal(200, first.GetProperty("y").GetInt32());
        var config = first.GetProperty("config");
        Assert.Equal(LightSource.DefaultBrightRadius * 5, config.GetProperty("bright").GetDouble());
        Assert.Equal(LightSource.DefaultDimRadius * 5, config.GetProperty("dim").GetDouble());
        Assert.Equal(LightSource.DefaultColor, config.GetProperty("color").GetString());
        var animation = config.GetProperty("animation");
        Assert.Equal("flame", animation.GetProperty("type").GetString());
        Assert.Equal(LightAnimations.DefaultSpeed, animation.GetProperty("speed").GetInt32());
    }

    [Fact]
    public async Task UvttExport_WritesActiveLightsInGridUnits()
    {
        var map = await ImportAsync(new InkarnateImportOptions());
        map.PreviewImagePng = [0x89, 0x50, 0x4E, 0x47];
        await using var output = new MemoryStream();

        await new UvttExporter().ExportAsync(map, output);

        output.Position = 0;
        using var document = await JsonDocument.ParseAsync(output);
        var lights = document.RootElement.GetProperty("lights");
        Assert.Equal(2, lights.GetArrayLength());
        Assert.Equal(1.5, lights[0].GetProperty("position").GetProperty("x").GetDouble(), precision: 6);
        Assert.Equal(LightSource.DefaultDimRadius, lights[0].GetProperty("range").GetDouble());
        Assert.Equal("ffffb15c", lights[0].GetProperty("color").GetString());
    }

    [Fact]
    public void Memento_RestoresLightChanges()
    {
        var map = new MapDocument { Lights = [new LightSource { EntityId = 1, DimRadius = 8 }] };
        var before = DocumentContentMemento.Capture(map);

        map.Lights[0].DimRadius = 3;
        map.Lights[0].IsActive = false;
        Assert.False(before.ContentEquals(DocumentContentMemento.Capture(map)));

        before.RestoreTo(map);

        Assert.Equal(8, map.Lights[0].DimRadius);
        Assert.True(map.Lights[0].IsActive);
    }

    private static async Task<MapDocument> ImportAsync(InkarnateImportOptions options)
    {
        const string json = """
            {
              "title": "lights",
              "version": 3,
              "scene": { "normSceneSize": { "w": 1000, "h": 1000 } },
              "history": [
                { "transactionId": 1, "cmdType": "cmd-layer-add", "layerId": "layer-a", "layerKind": "entity",
                  "layerData": { "name": "Props", "isVisible": true } },
                { "transactionId": 2, "cmdType": "cmd-layer-add", "layerId": "layer-h", "layerKind": "entity",
                  "layerData": { "name": "Hidden", "isVisible": false } },
                { "transactionId": 3, "cmdType": "cmd-entity-add", "items": [
                  { "layerId": "layer-grid", "entity": { "entityType": "grid", "entityId": 1, "style": { "size": 100 } } },
                  { "layerId": "layer-a", "entity": { "entityType": "stamp", "entityId": 10, "defaultName": "Hand Lantern Lit", "x": 100, "y": 200 } },
                  { "layerId": "layer-a", "entity": { "entityType": "stamp", "entityId": 11, "defaultName": "Oil Lantern Broken", "x": 1, "y": 1 } },
                  { "layerId": "layer-a", "entity": { "entityType": "stamp", "entityId": 12, "defaultName": "Crate", "x": 2, "y": 2 } },
                  { "layerId": "layer-h", "entity": { "entityType": "stamp", "entityId": 13, "defaultName": "Torch", "x": 3, "y": 3 } },
                  { "layerId": "layer-a", "entity": { "entityType": "stamp", "entityId": 14, "defaultName": "Candle", "x": 4, "y": 4 } },
                  { "layerId": "layer-a", "entity": { "entityType": "group", "entityId": 20, "x": 0, "y": 0 } },
                  { "layerId": "layer-a", "entity": { "entityType": "stamp", "entityId": 16, "defaultName": "Brazier", "x": 0, "y": 0 } }
                ] },
                { "transactionId": 4, "cmdType": "cmd-entity-update", "items": [ { "entityId": 10, "update": { "x": 150 } } ] },
                { "transactionId": 5, "cmdType": "cmd-entity-remove", "entityIds": [14] },
                { "transactionId": 6, "cmdType": "cmd-entity-group", "groupId": 20, "entityIds": [16] },
                { "transactionId": 7, "cmdType": "cmd-entity-update", "items": [ { "entityId": 20, "update": { "x": 50, "y": 0 } } ] }
              ]
            }
            """;

        var importer = new InkarnateImporter { Options = options };
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return await importer.ImportAsync(input);
    }
}
