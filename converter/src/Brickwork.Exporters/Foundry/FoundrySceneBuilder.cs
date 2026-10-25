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
        // Without a core version Foundry migrates the import as pre-levels data, and its levels
        // migration replaces "levels" with a single level built from "background". 14.353 is the
        // first version with levels, so no newer v14 refuses the import as too new.
        scene["_stats"] = new JsonObject { ["coreVersion"] = "14.353" };
        scene["name"] = map.Name;
        scene["width"] = canvas.Width;
        scene["height"] = canvas.Height;

        if (canvas.GridSize is int gridSize &&
            scene["grid"] is JsonObject grid)
        {
            grid["size"] = gridSize;
        }

        // Open at 100 % zoom (Foundry otherwise defaults to 50 / grid size); x/y stay null so it centers.
        if (scene["initial"] is JsonObject initial)
        {
            initial["scale"] = 1;
        }

        if (scene["environment"]?["globalLight"] is JsonObject globalLight)
        {
            globalLight["enabled"] = map.GlobalIllumination;
        }

        var backgroundSrc = FoundryPaths.BuildBackgroundSrc(
            map.FoundryMapsDirectory,
            map.SourceFileName,
            map.Name);
        scene["background"] = new JsonObject { ["src"] = backgroundSrc };
        // No thumb: Foundry then makes a small one when the scene is created. Pointing it at the
        // background made the Scenes sidebar decode the full-size map on hover.
        scene.Remove("thumb");

        var levels = new LevelLayout(Math.Clamp(map.SceneLevelCount, 1, SceneLevels.MaxCount), map.SceneLevelHeight);
        scene["levels"] = BuildLevels(scene["levels"]?[0] as JsonObject, levels, backgroundSrc);

        var wallSegments = FoundryWallSegmentBuilder.BuildFromMap(map);
        scene["walls"] = new JsonArray(wallSegments.Select(segment => ToWallJson(segment, levels)).ToArray());

        var regions = FoundryRegionBuilder.BuildFromMap(map);
        scene["regions"] = new JsonArray(regions.Select(region => ToRegionJson(region, levels)).ToArray());

        var lights = FoundryLightBuilder.BuildFromMap(map);
        // Foundry puts a light on the one level its elevation falls in (that is where it shines and
        // which walls block it), so a light on several levels becomes one light per level.
        scene["lights"] = new JsonArray(lights
            .SelectMany(light => levels.Count == 1
                ? [(light, Level: (int?)null)]
                : (light.Levels.Count == 0 ? Enumerable.Range(1, levels.Count) : light.Levels)
                    .Select(level => (light, Level: (int?)level)))
            .Select(pair => ToLightJson(pair.light, pair.Level, levels))
            .ToArray());

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

    /// <summary>Foundry levels stacked from the ground up, each <see cref="Height"/> feet tall.</summary>
    private sealed record LevelLayout(int Count, double Height)
    {
        /// <summary>Level 1 keeps the template's id (the scene's initial level).</summary>
        public static string Id(int level) => level == 1 ? "defaultLevel0000" : $"brickworkLevel{level:00}";

        public double Bottom(int level) => (level - 1) * Height;

        public double Top(int level) => level * Height;

        /// <summary>Foundry ids for an entity's levels; an empty list means every level.</summary>
        public JsonArray Ids(IReadOnlyList<int> levels) =>
            new(levels.Select(level => (JsonNode)JsonValue.Create(Id(level))!).ToArray());
    }

    /// <summary>
    /// One Foundry level per scene level. Only the first shows the map image; higher levels have no
    /// background of their own and show the levels below them.
    /// </summary>
    private static JsonArray BuildLevels(JsonObject? templateLevel, LevelLayout levels, string backgroundSrc)
    {
        var result = new JsonArray();
        for (var level = 1; level <= levels.Count; level++)
        {
            var node = templateLevel?.DeepClone().AsObject() ?? new JsonObject();
            node["_id"] = LevelLayout.Id(level);
            node["name"] = levels.Count == 1 ? "Level" : $"Level {level}";
            node["elevation"] = new JsonObject { ["bottom"] = levels.Bottom(level), ["top"] = levels.Top(level) };
            if (node["background"] is JsonObject background)
            {
                background["src"] = level == 1 ? backgroundSrc : null;
            }

            node["visibility"] = new JsonObject { ["levels"] = levels.Ids(Enumerable.Range(1, level - 1).ToList()) };
            node["sort"] = level - 1;
            result.Add(node);
        }

        return result;
    }

    private static JsonObject ToWallJson(FoundryWallSegment segment, LevelLayout levels)
    {
        return new JsonObject
        {
            ["levels"] = levels.Ids(segment.Levels),
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

    /// <param name="level">The level this copy of the light is on; null in a scene with one level.</param>
    private static JsonObject ToLightJson(FoundryLight light, int? level, LevelLayout levels) =>
        new()
        {
            ["_id"] = FoundryIdGenerator.Create(),
            ["x"] = light.X,
            ["y"] = light.Y,
            // On the floor of its level, so Foundry shows no relative elevation there.
            ["elevation"] = level is int floor ? levels.Bottom(floor) : 0,
            ["rotation"] = 0,
            ["levels"] = levels.Ids(level is int on ? [on] : []),
            ["walls"] = true,
            ["vision"] = false,
            ["config"] = new JsonObject
            {
                ["negative"] = false,
                ["priority"] = 0,
                ["alpha"] = 0.5,
                ["angle"] = 360,
                ["bright"] = light.BrightFeet,
                ["dim"] = light.DimFeet,
                // Foundry only skips colorization for a null color; black is our "no color".
                ["color"] = LightColors.IsNone(light.Color) ? null : light.Color,
                ["coloration"] = 1,
                ["attenuation"] = 0.5,
                ["luminosity"] = 0.5,
                ["saturation"] = 0,
                ["contrast"] = 0,
                ["shadows"] = 0,
                ["animation"] = new JsonObject
                {
                    ["type"] = light.AnimationType,
                    ["speed"] = light.AnimationSpeed,
                    ["intensity"] = light.AnimationIntensity,
                    ["reverse"] = false,
                },
                ["darkness"] = new JsonObject
                {
                    ["min"] = 0,
                    ["max"] = 1,
                },
            },
            ["hidden"] = false,
            ["flags"] = new JsonObject(),
        };

    private static JsonObject ToRegionJson(FoundryRegion region, LevelLayout levels)
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
                ["bottom"] = region.Levels.Count == 0 ? 0 : levels.Bottom(region.Levels.Min()),
                ["top"] = region.Levels.Count == 0 ? levels.Top(levels.Count) : levels.Top(region.Levels.Max()),
                ["topInclusive"] = false,
            },
            ["levels"] = levels.Ids(region.Levels),
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
