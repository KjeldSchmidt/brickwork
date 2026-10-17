using System.Text.Json;
using System.Text.Json.Nodes;
using Brickwork.Core.Export;
using Brickwork.Core.Foundry;
using Brickwork.Core.Models;

namespace Brickwork.Exporters.Foundry;

internal static class FoundrySceneBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public static async Task WriteAsync(MapDocument map, Stream destination, CancellationToken cancellationToken)
    {
        var canvas = FoundryExportCanvas.FromMap(map);

        var scene = LoadTemplate();
        scene["name"] = map.Name;
        scene["width"] = canvas.Width;
        scene["height"] = canvas.Height;

        if (canvas.GridSize is int gridSize &&
            scene["grid"] is JsonObject grid)
        {
            grid["size"] = gridSize;
        }

        var backgroundSrc = FoundryPaths.BuildBackgroundSrc(
            map.FoundryMapsDirectory,
            map.SourceFileName,
            map.Name);
        scene["background"] = new JsonObject { ["src"] = backgroundSrc };
        scene["thumb"] = backgroundSrc;

        var wallSegments = FoundryWallSegmentBuilder.BuildFromMap(map);
        scene["walls"] = new JsonArray(wallSegments.Select(ToWallJson).ToArray());

        var regions = FoundryRegionBuilder.BuildFromMap(map);
        scene["regions"] = new JsonArray(regions.Select(ToRegionJson).ToArray());

        await JsonSerializer.SerializeAsync(destination, scene, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    private static JsonObject LoadTemplate()
    {
        using var stream = typeof(FoundrySceneBuilder).Assembly
            .GetManifestResourceStream("Brickwork.Exporters.Foundry.FoundrySceneTemplate.json")
            ?? throw new InvalidOperationException("Missing embedded Foundry scene template.");

        return JsonNode.Parse(stream)?.AsObject()
            ?? throw new InvalidOperationException("Failed to parse Foundry scene template.");
    }

    private static JsonObject ToWallJson(FoundryWallSegment segment)
    {
        return new JsonObject
        {
            ["levels"] = new JsonArray("defaultLevel0000"),
            ["light"] = segment.Light,
            ["move"] = segment.Move,
            ["sight"] = segment.Sight,
            ["sound"] = segment.Sound,
            ["dir"] = 0,
            ["door"] = segment.Door,
            ["ds"] = 0,
            ["threshold"] = new JsonObject
            {
                ["light"] = segment.ThresholdLight,
                ["sight"] = segment.ThresholdSight,
                ["sound"] = segment.ThresholdSound,
                ["attenuation"] = segment.ThresholdAttenuation,
            },
            ["animation"] = null,
            ["flags"] = new JsonObject(),
            ["c"] = new JsonArray(segment.X0, segment.Y0, segment.X1, segment.Y1),
            ["_id"] = FoundryIdGenerator.Create(),
        };
    }

    private static JsonObject ToRegionJson(FoundryRegion region)
    {
        var behaviors = new JsonArray();
        if (region.WalkMovementCost is { } cost)
        {
            behaviors.Add(new JsonObject
            {
                ["name"] = region.Name,
                ["type"] = "modifyMovementCost",
                ["_id"] = FoundryIdGenerator.Create(),
                ["system"] = new JsonObject
                {
                    ["difficulties"] = new JsonObject
                    {
                        ["walk"] = cost,
                        ["fly"] = 1,
                        ["swim"] = 1,
                        ["burrow"] = 1,
                        ["deploy"] = cost,
                        ["travel"] = cost,
                    },
                },
                ["disabled"] = false,
                ["flags"] = new JsonObject(),
            });
        }

        return new JsonObject
        {
            ["name"] = region.Name,
            ["_id"] = FoundryIdGenerator.Create(),
            ["color"] = region.Color,
            ["shapes"] = new JsonArray(region.Shapes.Select(ToShapeJson).ToArray()),
            ["elevation"] = new JsonObject
            {
                ["bottom"] = 0,
                ["top"] = 20,
                ["topInclusive"] = false,
            },
            ["levels"] = new JsonArray("defaultLevel0000"),
            ["restriction"] = new JsonObject
            {
                ["enabled"] = false,
                ["type"] = "move",
                ["priority"] = 0,
            },
            ["attachment"] = new JsonObject
            {
                ["token"] = null,
            },
            ["behaviors"] = behaviors,
            ["visibility"] = 4,
            ["highlightMode"] = "shapes",
            ["displayMeasurements"] = false,
            ["hidden"] = false,
            ["locked"] = false,
            ["flags"] = new JsonObject(),
            ["_shapeConstraints"] = null,
            ["ownership"] = new JsonObject
            {
                ["default"] = 0,
            },
        };
    }

    private static JsonObject ToShapeJson(FoundryRegionPolygon shape) =>
        new()
        {
            ["type"] = "polygon",
            ["points"] = new JsonArray(shape.Points.Select(point => (JsonNode)point).ToArray()),
            ["hole"] = false,
            ["origin"] = null,
        };
}
