using System.Text.Json;
using Brickwork.Core.Models;

namespace Brickwork.Inkarnate.Parsing;

internal static class InkarnateDocumentParser
{
    private static readonly InkTransactionRegistry Registry = InkTransactionRegistry.CreateDefault();

    public static MapDocument Parse(JsonElement root)
    {
        var map = new MapDocument
        {
            Name = InkJsonReader.ReadString(root, "title") ?? "Untitled Map",
            SourceVersion = InkJsonReader.ReadInt(root, "version"),
        };

        if (root.TryGetProperty("scene", out var sceneElement))
        {
            map.Scene = ReadSceneDimensions(sceneElement);
        }

        if (root.TryGetProperty("previewDimensions", out var previewElement))
        {
            map.Preview = ReadPreviewDimensions(previewElement);
        }

        map.PreviewImagePng = InkPreviewImageReader.ReadPreviewImagePng(root);

        var context = new InkImportContext(map);

        if (root.TryGetProperty("history", out var historyElement) &&
            historyElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var transaction in historyElement.EnumerateArray())
            {
                context.Transactions.Add(Registry.Process(context, transaction));
            }
        }

        ApplyDerivedGridMetrics(map);
        context.SyncWalls();

        map.Compatibility = new CompatibilityReport
        {
            MapTitle = map.Name,
            SourceVersion = map.SourceVersion,
            Transactions = context.Transactions.ToList(),
        };

        return map;
    }

    private static SceneDimensions ReadSceneDimensions(JsonElement sceneElement)
    {
        if (!sceneElement.TryGetProperty("normSceneSize", out var sizeElement))
        {
            return new SceneDimensions();
        }

        return new SceneDimensions
        {
            Width = InkJsonReader.ReadDouble(sizeElement, "w"),
            Height = InkJsonReader.ReadDouble(sizeElement, "h"),
        };
    }

    private static PreviewDimensions ReadPreviewDimensions(JsonElement previewElement) =>
        new()
        {
            Width = InkJsonReader.ReadInt(previewElement, "w") ?? 0,
            Height = InkJsonReader.ReadInt(previewElement, "h") ?? 0,
        };

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
}
