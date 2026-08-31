using System.Text.Json;
using InkarnateTools.Core.Models;
using InkarnateTools.Inkarnate.Parsing;

namespace InkarnateTools.Inkarnate.Handlers;

internal sealed class GridEntityHandler : IInkEntityHandler
{
    public string EntityType => "grid";

    public TransactionUnderstanding Understanding => TransactionUnderstanding.FullyUnderstood;

    public void Apply(MapDocument map, JsonElement entity)
    {
        if (!entity.TryGetProperty("style", out var styleElement))
        {
            return;
        }

        var cellSize = InkJsonReader.ReadDouble(styleElement, "size");
        if (cellSize <= 0)
        {
            return;
        }

        map.Grid.CellSize = cellSize;
    }
}

internal sealed class WallEntityHandler : IInkEntityHandler
{
    public string EntityType => "wall";

    public TransactionUnderstanding Understanding => TransactionUnderstanding.FullyUnderstood;

    public void Apply(MapDocument map, JsonElement entity)
    {
        if (!entity.TryGetProperty("points", out var pointsElement) ||
            pointsElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var segment = new WallSegment();
        foreach (var pointElement in pointsElement.EnumerateArray())
        {
            segment.Points.Add(EntityParsing.ReadPoint(pointElement));
        }

        if (segment.Points.Count > 0)
        {
            map.Walls.Add(segment);
        }
    }
}

internal sealed class LightEntityHandler : IInkEntityHandler
{
    public string EntityType => "light";

    public TransactionUnderstanding Understanding => TransactionUnderstanding.FullyUnderstood;

    public void Apply(MapDocument map, JsonElement entity)
    {
        var light = new LightSource
        {
            Range = InkJsonReader.ReadDouble(entity, "range"),
            Color = EntityParsing.ReadColor(entity),
        };

        if (entity.TryGetProperty("position", out var positionElement))
        {
            light.Position = EntityParsing.ReadPoint(positionElement);
        }

        map.Lights.Add(light);
    }
}

internal static class EntityParsing
{
    public static MapPoint ReadPoint(JsonElement pointElement) =>
        new(InkJsonReader.ReadDouble(pointElement, "x"), InkJsonReader.ReadDouble(pointElement, "y"));

    public static string ReadColor(JsonElement entityElement)
    {
        if (!entityElement.TryGetProperty("color", out var colorElement))
        {
            return "#ffffff";
        }

        var red = (int)Math.Clamp(InkJsonReader.ReadDouble(colorElement, "r"), 0, 255);
        var green = (int)Math.Clamp(InkJsonReader.ReadDouble(colorElement, "g"), 0, 255);
        var blue = (int)Math.Clamp(InkJsonReader.ReadDouble(colorElement, "b"), 0, 255);
        return $"#{red:X2}{green:X2}{blue:X2}";
    }
}
