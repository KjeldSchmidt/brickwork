using System.Text.Json;
using Brickwork.Core.Export;
using Brickwork.Core.Foundry;
using Brickwork.Core.Geometry;
using Brickwork.Core.Models;

namespace Brickwork.Exporters.Foundry;

public sealed record FoundrySceneMapContext(
    string? DataRoot = null,
    string? FoundryMapsDirectory = null,
    double WallSimplificationTolerance = WallSimplificationSettings.DefaultToleranceSceneUnits);

/// <summary>
/// Maps an assembled Foundry scene JSON document into a <see cref="MapDocument"/>.
/// Pure JSON → model; does not touch LevelDB.
/// </summary>
public static class FoundrySceneMapper
{
    private static readonly string[] DifficultTerrainColors =
    [
        "#cc7428",
        "#cc9528",
    ];

    private static readonly string[] GreaterDifficultTerrainColors =
    [
        "#6d28cc",
    ];

    public static MapDocument MapFromJson(string sceneJson, FoundrySceneMapContext? context = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sceneJson);
        using var document = JsonDocument.Parse(sceneJson);
        return MapFromElement(document.RootElement, context);
    }

    public static MapDocument MapFromElement(JsonElement root, FoundrySceneMapContext? context = null)
    {
        context ??= new FoundrySceneMapContext();

        var width = root.TryGetProperty("width", out var widthEl) && widthEl.TryGetInt32(out var w) ? w : 0;
        var height = root.TryGetProperty("height", out var heightEl) && heightEl.TryGetInt32(out var h) ? h : 0;
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("Foundry scene is missing positive width/height.");
        }

        var gridSize = 100;
        if (root.TryGetProperty("grid", out var gridEl))
        {
            if (gridEl.ValueKind == JsonValueKind.Object &&
                gridEl.TryGetProperty("size", out var sizeEl) &&
                sizeEl.TryGetInt32(out var size) &&
                size > 0)
            {
                gridSize = size;
            }
            else if (gridEl.TryGetInt32(out var legacySize) && legacySize > 0)
            {
                gridSize = legacySize;
            }
        }

        var columns = Math.Max(1, (int)Math.Round(width / (double)gridSize));
        var rows = Math.Max(1, (int)Math.Round(height / (double)gridSize));
        var origin = FoundrySceneDimensions.ReadOrigin(root, gridSize);
        var defaultLevelId = FoundrySceneDimensions.ResolveDefaultLevelId(root);

        var name = root.TryGetProperty("name", out var nameEl)
            ? nameEl.GetString()
            : null;

        // Scene space = Foundry scene rectangle (background) pixels, 1:1 with Preview.
        // Wall/region canvas coords are converted by subtracting sceneX/sceneY padding origin.
        var map = new MapDocument
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Foundry Scene" : name!,
            Scene = new SceneDimensions { Width = width, Height = height },
            Preview = new PreviewDimensions { Width = width, Height = height },
            FoundryMapsDirectory = context.FoundryMapsDirectory,
            Grid = new GridInfo
            {
                // One scene unit = one pixel; grid.size is pixels-per-cell on the preview.
                CellSize = 1,
                PixelsPerCell = gridSize,
                Columns = columns,
                Rows = rows,
            },
        };

        var defaultLayer = MapLayerEditing.EnsureDefaultLayer(map);
        var entityId = 1;
        var rawWalls = new List<Wall>();

        if (root.TryGetProperty("walls", out var wallsEl) && wallsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var wallEl in wallsEl.EnumerateArray())
            {
                if (wallEl.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!FoundrySceneDimensions.BelongsToDefaultLevel(wallEl, defaultLevelId))
                {
                    continue;
                }

                var wall = TryMapWall(wallEl, defaultLayer.Id, origin, ref entityId);
                if (wall is not null)
                {
                    rawWalls.Add(wall);
                }
            }
        }

        foreach (var wall in WallJoining.Join(rawWalls, startingEntityId: 1))
        {
            map.Walls.Add(wall);
        }

        WallPointSimplifier.ApplyAll(map.Walls, context.WallSimplificationTolerance);

        entityId = map.Walls.Count == 0
            ? 1
            : map.Walls.Max(wall => wall.EntityId) + 1;

        if (root.TryGetProperty("regions", out var regionsEl) && regionsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var regionEl in regionsEl.EnumerateArray())
            {
                if (regionEl.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!FoundrySceneDimensions.BelongsToDefaultLevel(regionEl, defaultLevelId))
                {
                    continue;
                }

                foreach (var region in MapRegionShapes(regionEl, defaultLayer.Id, origin, entityId))
                {
                    map.Regions.Add(region);
                    entityId = Math.Max(entityId, region.EntityId + 1);
                }
            }
        }

        ApplyBackground(map, root, context);
        return map;
    }

    private static Wall? TryMapWall(
        JsonElement wallEl,
        string layerId,
        FoundrySceneOrigin origin,
        ref int entityId)
    {
        if (!wallEl.TryGetProperty("c", out var coords) ||
            coords.ValueKind != JsonValueKind.Array ||
            coords.GetArrayLength() < 4)
        {
            return null;
        }

        if (!TryGetCoord(coords[0], out var x0) ||
            !TryGetCoord(coords[1], out var y0) ||
            !TryGetCoord(coords[2], out var x1) ||
            !TryGetCoord(coords[3], out var y1))
        {
            return null;
        }

        x0 -= origin.SceneX;
        y0 -= origin.SceneY;
        x1 -= origin.SceneX;
        y1 -= origin.SceneY;

        var restrictions = ReadRestrictions(wallEl);
        var lineType = FoundryWallRestrictions.InferLineType(restrictions);
        var id = entityId++;

        return new Wall
        {
            EntityId = id,
            Name = $"Wall {id}",
            LayerId = layerId,
            LineType = lineType,
            Origin = new MapPoint(x0, y0),
            PathOrigin = new MapPoint(x0, y0),
            RotationPivot = new MapPoint(x0, y0),
            Points =
            [
                new MapPoint(x0, y0),
                new MapPoint(x1, y1),
            ],
        };
    }

    private static FoundryWallRestrictions ReadRestrictions(JsonElement wallEl)
    {
        var sight = GetInt(wallEl, "sight", 20);
        var light = GetInt(wallEl, "light", 20);
        var sound = GetInt(wallEl, "sound", 20);
        var move = GetInt(wallEl, "move", 20);
        var door = GetInt(wallEl, "door", 0);

        int? thresholdLight = null;
        int? thresholdSight = null;
        int? thresholdSound = null;
        var attenuation = false;

        if (wallEl.TryGetProperty("threshold", out var threshold) &&
            threshold.ValueKind == JsonValueKind.Object)
        {
            thresholdLight = GetNullableInt(threshold, "light");
            thresholdSight = GetNullableInt(threshold, "sight");
            thresholdSound = GetNullableInt(threshold, "sound");
            if (threshold.TryGetProperty("attenuation", out var att) &&
                att.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                attenuation = att.GetBoolean();
            }
        }

        return new FoundryWallRestrictions(
            sight,
            light,
            sound,
            move,
            door,
            thresholdLight,
            thresholdSight,
            thresholdSound,
            attenuation);
    }

    private static IReadOnlyList<Region> MapRegionShapes(
        JsonElement regionEl,
        string layerId,
        FoundrySceneOrigin origin,
        int nextEntityId)
    {
        if (!regionEl.TryGetProperty("shapes", out var shapesEl) ||
            shapesEl.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var regionType = InferRegionType(regionEl);
        var baseName = regionEl.TryGetProperty("name", out var nameEl)
            ? nameEl.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = regionType.ToDisplayName();
        }

        var results = new List<Region>();
        var shapeIndex = 0;
        var entityId = nextEntityId;
        foreach (var shapeEl in shapesEl.EnumerateArray())
        {
            if (shapeEl.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (shapeEl.TryGetProperty("hole", out var holeEl) &&
                holeEl.ValueKind == JsonValueKind.True)
            {
                continue;
            }

            var points = TryReadShapePoints(shapeEl, origin);
            if (points is null || points.Count < 3)
            {
                continue;
            }

            shapeIndex++;
            var id = entityId++;
            var name = shapeIndex == 1 ? baseName! : $"{baseName} ({shapeIndex})";

            results.Add(new Region
            {
                EntityId = id,
                Name = name,
                LayerId = layerId,
                RegionType = regionType,
                Points = points,
            });
        }

        return results;
    }

    private static RegionType InferRegionType(JsonElement regionEl)
    {
        var walkCost = TryReadWalkMovementCost(regionEl);
        if (walkCost == 2)
        {
            return RegionType.DifficultTerrain;
        }

        if (walkCost == 3)
        {
            return RegionType.GreaterDifficultTerrain;
        }

        if (regionEl.TryGetProperty("color", out var colorEl))
        {
            var color = NormalizeColor(colorEl.GetString());
            if (color is not null)
            {
                if (GreaterDifficultTerrainColors.Any(c =>
                        string.Equals(c, color, StringComparison.OrdinalIgnoreCase)))
                {
                    return RegionType.GreaterDifficultTerrain;
                }

                if (DifficultTerrainColors.Any(c =>
                        string.Equals(c, color, StringComparison.OrdinalIgnoreCase)))
                {
                    return RegionType.DifficultTerrain;
                }
            }
        }

        return RegionType.Other;
    }

    private static int? TryReadWalkMovementCost(JsonElement regionEl)
    {
        if (!regionEl.TryGetProperty("behaviors", out var behaviors) ||
            behaviors.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var behavior in behaviors.EnumerateArray())
        {
            if (behavior.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var type = behavior.TryGetProperty("type", out var typeEl)
                ? typeEl.GetString()
                : null;
            if (!string.Equals(type, "modifyMovementCost", StringComparison.Ordinal))
            {
                continue;
            }

            if (behavior.TryGetProperty("disabled", out var disabled) &&
                disabled.ValueKind == JsonValueKind.True)
            {
                continue;
            }

            if (behavior.TryGetProperty("system", out var system) &&
                system.ValueKind == JsonValueKind.Object &&
                system.TryGetProperty("difficulties", out var difficulties) &&
                difficulties.ValueKind == JsonValueKind.Object &&
                difficulties.TryGetProperty("walk", out var walk) &&
                walk.TryGetInt32(out var walkCost))
            {
                return walkCost;
            }
        }

        return null;
    }

    private static List<MapPoint>? TryReadShapePoints(JsonElement shapeEl, FoundrySceneOrigin origin)
    {
        var type = shapeEl.TryGetProperty("type", out var typeEl)
            ? typeEl.GetString()
            : null;

        if (string.Equals(type, "rectangle", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetDouble(shapeEl, "x", out var x) ||
                !TryGetDouble(shapeEl, "y", out var y) ||
                !TryGetDouble(shapeEl, "width", out var width) ||
                !TryGetDouble(shapeEl, "height", out var height))
            {
                return null;
            }

            x -= origin.SceneX;
            y -= origin.SceneY;

            return
            [
                new MapPoint(x, y),
                new MapPoint(x + width, y),
                new MapPoint(x + width, y + height),
                new MapPoint(x, y + height),
            ];
        }

        if (string.Equals(type, "polygon", StringComparison.OrdinalIgnoreCase) ||
            type is null)
        {
            if (!shapeEl.TryGetProperty("points", out var pointsEl) ||
                pointsEl.ValueKind != JsonValueKind.Array ||
                pointsEl.GetArrayLength() < 6)
            {
                return null;
            }

            var points = new List<MapPoint>();
            var length = pointsEl.GetArrayLength();
            for (var i = 0; i + 1 < length; i += 2)
            {
                if (!TryGetCoord(pointsEl[i], out var px) ||
                    !TryGetCoord(pointsEl[i + 1], out var py))
                {
                    return null;
                }

                points.Add(new MapPoint(px - origin.SceneX, py - origin.SceneY));
            }

            return points.Count >= 3 ? points : null;
        }

        if (string.Equals(type, "ellipse", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetDouble(shapeEl, "x", out var x) ||
                !TryGetDouble(shapeEl, "y", out var y) ||
                !TryGetDouble(shapeEl, "radiusX", out var rx) ||
                !TryGetDouble(shapeEl, "radiusY", out var ry) ||
                rx <= 0 ||
                ry <= 0)
            {
                return null;
            }

            return ApproximateEllipse(x - origin.SceneX, y - origin.SceneY, rx, ry);
        }

        if (string.Equals(type, "circle", StringComparison.OrdinalIgnoreCase))
        {
            // Foundry MeasuredTemplate / region circle: x/y are center.
            if (!TryGetDouble(shapeEl, "x", out var x) ||
                !TryGetDouble(shapeEl, "y", out var y) ||
                !TryGetDouble(shapeEl, "radius", out var radius) ||
                radius <= 0)
            {
                return null;
            }

            return ApproximateEllipse(x - origin.SceneX, y - origin.SceneY, radius, radius);
        }

        return null;
    }

    private const int EllipseApproximationSegments = 32;

    private static List<MapPoint> ApproximateEllipse(double centerX, double centerY, double radiusX, double radiusY)
    {
        var points = new List<MapPoint>(EllipseApproximationSegments);
        for (var i = 0; i < EllipseApproximationSegments; i++)
        {
            var angle = i * (Math.PI * 2d / EllipseApproximationSegments);
            points.Add(new MapPoint(
                centerX + (radiusX * Math.Cos(angle)),
                centerY + (radiusY * Math.Sin(angle))));
        }

        return points;
    }

    private static void ApplyBackground(
        MapDocument map,
        JsonElement root,
        FoundrySceneMapContext context)
    {
        var src = TryReadBackgroundSrc(root);
        if (string.IsNullOrWhiteSpace(src) || string.IsNullOrWhiteSpace(context.DataRoot))
        {
            return;
        }

        var absolutePath = ResolveBackgroundPath(context.DataRoot!, src);
        if (absolutePath is null || !File.Exists(absolutePath))
        {
            return;
        }

        try
        {
            map.PreviewImagePng = File.ReadAllBytes(absolutePath);
            map.ImagePath = absolutePath;

            var fileName = Path.GetFileName(absolutePath);
            map.UnderlyingMap = new UnderlyingMapInfo
            {
                FileName = fileName,
                AbsolutePath = absolutePath,
                Width = (int)Math.Round(map.Scene.Width),
                Height = (int)Math.Round(map.Scene.Height),
            };
        }
        catch
        {
            // Background is optional for editing walls/regions.
        }
    }

    private static string? TryReadBackgroundSrc(JsonElement root)
    {
        if (root.TryGetProperty("background", out var background) &&
            background.ValueKind == JsonValueKind.Object &&
            background.TryGetProperty("src", out var srcEl))
        {
            var src = srcEl.GetString();
            if (!string.IsNullOrWhiteSpace(src))
            {
                return src;
            }
        }

        if (root.TryGetProperty("levels", out var levels) &&
            levels.ValueKind == JsonValueKind.Array)
        {
            foreach (var level in levels.EnumerateArray())
            {
                if (level.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (level.TryGetProperty("background", out var levelBg) &&
                    levelBg.ValueKind == JsonValueKind.Object &&
                    levelBg.TryGetProperty("src", out var levelSrcEl))
                {
                    var levelSrc = levelSrcEl.GetString();
                    if (!string.IsNullOrWhiteSpace(levelSrc))
                    {
                        return levelSrc;
                    }
                }
            }
        }

        return null;
    }

    public static string? ResolveBackgroundPath(string dataRoot, string foundrySrc)
    {
        var relative = Uri.UnescapeDataString(foundrySrc.Replace('\\', '/').TrimStart('/'));
        if (string.IsNullOrWhiteSpace(relative))
        {
            return null;
        }

        // Reject absolute / URL schemes that are not Data-relative paths.
        if (relative.Contains("://", StringComparison.Ordinal) ||
            Path.IsPathRooted(relative))
        {
            // Windows rooted paths are unexpected for Foundry src; still allow under dataRoot only
            // when they resolve inside it.
            if (Path.IsPathRooted(relative))
            {
                try
                {
                    var rooted = Path.GetFullPath(relative);
                    var dataFull = Path.GetFullPath(dataRoot);
                    if (rooted.StartsWith(dataFull, StringComparison.OrdinalIgnoreCase))
                    {
                        return rooted;
                    }
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        try
        {
            var combined = Path.GetFullPath(Path.Combine(dataRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            var dataFull = Path.GetFullPath(dataRoot);
            if (!combined.StartsWith(dataFull, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return combined;
        }
        catch
        {
            return null;
        }
    }

    private static string? NormalizeColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return null;
        }

        color = color.Trim();
        if (!color.StartsWith('#') && color.Length is 6 or 8)
        {
            color = "#" + color;
        }

        return color.Length >= 7 ? color[..7] : color;
    }

    private static bool TryGetCoord(JsonElement element, out double value)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryGetDouble(JsonElement parent, string name, out double value)
    {
        value = 0;
        return parent.TryGetProperty(name, out var el) &&
               el.ValueKind == JsonValueKind.Number &&
               el.TryGetDouble(out value);
    }

    private static int GetInt(JsonElement parent, string name, int fallback)
    {
        if (parent.TryGetProperty(name, out var el) &&
            el.ValueKind == JsonValueKind.Number &&
            el.TryGetInt32(out var value))
        {
            return value;
        }

        return fallback;
    }

    private static int? GetNullableInt(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var el) || el.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var value)
            ? value
            : null;
    }
}
