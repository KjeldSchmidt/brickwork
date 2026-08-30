using System.IO.Compression;
using System.Text.Json;
using InkarnateTools.Core.Models;
using InkarnateTools.Core.Ports;

namespace InkarnateTools.Inkarnate;

public sealed class InkarnateImporter : IMapImporter
{
    public string FormatId => "inkarnate";

    public async Task<MapDocument> ImportAsync(Stream source, CancellationToken cancellationToken = default)
    {
        await using var jsonStream = await OpenJsonStreamAsync(source, cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(jsonStream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return ParseMapDocument(document.RootElement);
    }

    internal static MapDocument ParseMapDocument(JsonElement root)
    {
        var map = new MapDocument
        {
            Name = ReadString(root, "title") ?? "Untitled Map",
            SourceVersion = ReadInt(root, "version"),
        };

        if (root.TryGetProperty("scene", out var sceneElement))
        {
            map.Scene = ReadSceneDimensions(sceneElement);
        }

        if (root.TryGetProperty("previewDimensions", out var previewElement))
        {
            map.Preview = ReadPreviewDimensions(previewElement);
        }

        if (root.TryGetProperty("history", out var historyElement) &&
            historyElement.ValueKind == JsonValueKind.Array)
        {
            ApplyHistory(map, historyElement);
        }

        ApplyDerivedGridMetrics(map);
        return map;
    }

    private static async Task<Stream> OpenJsonStreamAsync(Stream source, CancellationToken cancellationToken)
    {
        var buffer = source;

        if (!source.CanSeek)
        {
            var memoryStream = new MemoryStream();
            await source.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
            memoryStream.Position = 0;
            buffer = memoryStream;
        }

        if (IsGZip(buffer))
        {
            buffer.Position = 0;
            return new GZipStream(buffer, CompressionMode.Decompress, leaveOpen: true);
        }

        buffer.Position = 0;
        return buffer;
    }

    private static bool IsGZip(Stream stream)
    {
        Span<byte> header = stackalloc byte[2];
        var read = stream.Read(header);
        stream.Position = 0;
        return read == 2 && header[0] == 0x1f && header[1] == 0x8b;
    }

    private static SceneDimensions ReadSceneDimensions(JsonElement sceneElement)
    {
        if (!sceneElement.TryGetProperty("normSceneSize", out var sizeElement))
        {
            return new SceneDimensions();
        }

        return new SceneDimensions
        {
            Width = ReadDouble(sizeElement, "w"),
            Height = ReadDouble(sizeElement, "h"),
        };
    }

    private static PreviewDimensions ReadPreviewDimensions(JsonElement previewElement) =>
        new()
        {
            Width = ReadInt(previewElement, "w") ?? 0,
            Height = ReadInt(previewElement, "h") ?? 0,
        };

    private static void ApplyHistory(MapDocument map, JsonElement historyElement)
    {
        foreach (var command in historyElement.EnumerateArray())
        {
            if (!TryGetString(command, "cmdType", out var cmdType))
            {
                continue;
            }

            if (cmdType == "cmd-entity-add")
            {
                ApplyEntityAddCommand(map, command);
            }
        }
    }

    private static void ApplyEntityAddCommand(MapDocument map, JsonElement command)
    {
        if (!command.TryGetProperty("items", out var itemsElement) ||
            itemsElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in itemsElement.EnumerateArray())
        {
            if (!item.TryGetProperty("entity", out var entityElement))
            {
                continue;
            }

            var entityType = ReadString(entityElement, "entityType");
            switch (entityType)
            {
                case "grid":
                    ApplyGridEntity(map, entityElement);
                    break;
                case "wall":
                    ApplyWallEntity(map, entityElement);
                    break;
                case "light":
                    ApplyLightEntity(map, entityElement);
                    break;
            }
        }
    }

    private static void ApplyGridEntity(MapDocument map, JsonElement entityElement)
    {
        if (!entityElement.TryGetProperty("style", out var styleElement))
        {
            return;
        }

        var cellSize = ReadDouble(styleElement, "size");
        if (cellSize <= 0)
        {
            return;
        }

        map.Grid.CellSize = cellSize;
    }

    private static void ApplyWallEntity(MapDocument map, JsonElement entityElement)
    {
        if (!entityElement.TryGetProperty("points", out var pointsElement) ||
            pointsElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var segment = new WallSegment();
        foreach (var pointElement in pointsElement.EnumerateArray())
        {
            segment.Points.Add(ReadPoint(pointElement));
        }

        if (segment.Points.Count > 0)
        {
            map.Walls.Add(segment);
        }
    }

    private static void ApplyLightEntity(MapDocument map, JsonElement entityElement)
    {
        var light = new LightSource
        {
            Range = ReadDouble(entityElement, "range"),
            Color = ReadColor(entityElement),
        };

        if (entityElement.TryGetProperty("position", out var positionElement))
        {
            light.Position = ReadPoint(positionElement);
        }

        map.Lights.Add(light);
    }

    private static void ApplyDerivedGridMetrics(MapDocument map)
    {
        if (map.Grid.CellSize <= 0 || map.Scene.Width <= 0 || map.Scene.Height <= 0)
        {
            return;
        }

        map.Grid.Columns = (int)Math.Round(map.Scene.Width / map.Grid.CellSize);
        map.Grid.Rows = (int)Math.Round(map.Scene.Height / map.Grid.CellSize);

        if (map.Preview is { Width: > 0, Height: > 0 } &&
            map.Grid.Columns > 0 &&
            map.Grid.Rows > 0)
        {
            var pixelsPerCellX = map.Preview.Width / (double)map.Grid.Columns;
            var pixelsPerCellY = map.Preview.Height / (double)map.Grid.Rows;
            map.Grid.PixelsPerCell = (int)Math.Round((pixelsPerCellX + pixelsPerCellY) / 2d);
        }
    }

    private static MapPoint ReadPoint(JsonElement pointElement) =>
        new(ReadDouble(pointElement, "x"), ReadDouble(pointElement, "y"));

    private static string ReadColor(JsonElement entityElement)
    {
        if (!entityElement.TryGetProperty("color", out var colorElement))
        {
            return "#ffffff";
        }

        var red = (int)Math.Clamp(ReadDouble(colorElement, "r"), 0, 255);
        var green = (int)Math.Clamp(ReadDouble(colorElement, "g"), 0, 255);
        var blue = (int)Math.Clamp(ReadDouble(colorElement, "b"), 0, 255);
        return $"#{red:X2}{green:X2}{blue:X2}";
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var valueElement) &&
        valueElement.ValueKind == JsonValueKind.String
            ? valueElement.GetString()
            : null;

    private static int? ReadInt(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var valueElement) &&
        valueElement.ValueKind == JsonValueKind.Number &&
        valueElement.TryGetInt32(out var value)
            ? value
            : null;

    private static double ReadDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var valueElement) ||
            valueElement.ValueKind != JsonValueKind.Number)
        {
            return 0;
        }

        return valueElement.GetDouble();
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = ReadString(element, propertyName) ?? string.Empty;
        return !string.IsNullOrEmpty(value);
    }
}
