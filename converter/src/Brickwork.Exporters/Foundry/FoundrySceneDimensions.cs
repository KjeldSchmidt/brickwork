using System.Text.Json;

namespace Brickwork.Exporters.Foundry;

/// <summary>
/// Foundry canvas vs scene-rectangle geometry (square/gridless padding), matching
/// <c>SquareGrid#calculateDimensions</c> / <c>Scene#getDimensions</c>.
/// </summary>
public readonly record struct FoundrySceneOrigin(double SceneX, double SceneY);

public static class FoundrySceneDimensions
{
    /// <summary>
    /// Computes the top-left of the scene rectangle within the padded canvas
    /// (<c>sceneX</c>/<c>sceneY</c>), including <c>shiftX</c>/<c>shiftY</c>.
    /// </summary>
    public static FoundrySceneOrigin ComputeOrigin(
        double sceneWidth,
        double sceneHeight,
        double padding,
        int gridSize,
        double shiftX = 0,
        double shiftY = 0)
    {
        var size = Math.Max(1, gridSize);
        // Note: Foundry intentionally uses *(1/size) instead of /size for stability.
        var x = Math.Ceiling((padding * sceneWidth) * (1d / size)) * size;
        var y = Math.Ceiling((padding * sceneHeight) * (1d / size)) * size;
        return new FoundrySceneOrigin(x - shiftX, y - shiftY);
    }

    public static FoundrySceneOrigin ReadOrigin(JsonElement root, int gridSize)
    {
        var width = root.TryGetProperty("width", out var w) && w.TryGetDouble(out var wd) ? wd : 0;
        var height = root.TryGetProperty("height", out var h) && h.TryGetDouble(out var hd) ? hd : 0;
        var padding = root.TryGetProperty("padding", out var p) && p.TryGetDouble(out var pd) ? pd : 0;
        var shiftX = root.TryGetProperty("shiftX", out var sx) && sx.TryGetDouble(out var sxd) ? sxd : 0;
        var shiftY = root.TryGetProperty("shiftY", out var sy) && sy.TryGetDouble(out var syd) ? syd : 0;
        return ComputeOrigin(width, height, padding, gridSize, shiftX, shiftY);
    }

    /// <summary>
    /// Resolves the default/initial level id used when levels support is not yet implemented.
    /// </summary>
    public static string? ResolveDefaultLevelId(JsonElement root)
    {
        if (root.TryGetProperty("initialLevel", out var initial) &&
            initial.ValueKind == JsonValueKind.String)
        {
            var id = initial.GetString();
            if (!string.IsNullOrWhiteSpace(id))
            {
                return id;
            }
        }

        if (root.TryGetProperty("levels", out var levels) &&
            levels.ValueKind == JsonValueKind.Array &&
            levels.GetArrayLength() > 0)
        {
            var first = levels[0];
            if (first.ValueKind == JsonValueKind.String)
            {
                return first.GetString();
            }

            if (first.ValueKind == JsonValueKind.Object &&
                first.TryGetProperty("_id", out var embeddedId))
            {
                return embeddedId.GetString();
            }
        }

        return "defaultLevel0000";
    }

    /// <summary>
    /// True when a placeable belongs on the default level: empty/missing <c>levels</c>
    /// (all levels) or explicitly lists <paramref name="defaultLevelId"/>.
    /// </summary>
    public static bool BelongsToDefaultLevel(JsonElement placeable, string? defaultLevelId)
    {
        if (!placeable.TryGetProperty("levels", out var levels) ||
            levels.ValueKind != JsonValueKind.Array)
        {
            return true;
        }

        if (levels.GetArrayLength() == 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(defaultLevelId))
        {
            return true;
        }

        foreach (var level in levels.EnumerateArray())
        {
            if (level.ValueKind == JsonValueKind.String &&
                string.Equals(level.GetString(), defaultLevelId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
