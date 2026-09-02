using System.Text.Json;
using System.Text.Json.Nodes;
using InkarnateTools.Core.Export;
using InkarnateTools.Core.Models;

namespace InkarnateTools.Exporters.Foundry;

internal static class FoundrySceneBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public static async Task WriteAsync(MapDocument map, Stream destination, CancellationToken cancellationToken)
    {
        var preview = map.Preview
            ?? throw new InvalidOperationException("Map preview dimensions are required for Foundry export.");

        if (preview.Width <= 0 || preview.Height <= 0)
        {
            throw new InvalidOperationException("Map preview dimensions must be positive for Foundry export.");
        }

        var scene = LoadTemplate();
        scene["name"] = map.Name;
        scene["width"] = preview.Width;
        scene["height"] = preview.Height;

        var backgroundSrc = Path.ChangeExtension(map.SourceFileName ?? "map.ink", ".webp");
        scene["levels"]![0]!["background"]!["src"] = backgroundSrc;

        var wallSegments = FoundryWallSegmentBuilder.BuildFromMap(map);
        scene["walls"] = new JsonArray(wallSegments.Select(ToWallJson).ToArray());

        await JsonSerializer.SerializeAsync(destination, scene, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    private static JsonObject LoadTemplate()
    {
        using var stream = typeof(FoundrySceneBuilder).Assembly
            .GetManifestResourceStream("InkarnateTools.Exporters.Foundry.FoundrySceneTemplate.json")
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
}
