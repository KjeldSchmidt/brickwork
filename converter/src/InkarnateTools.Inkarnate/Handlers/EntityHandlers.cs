using System.Text.Json;
using InkarnateTools.Core.Models;
using InkarnateTools.Inkarnate.Parsing;

namespace InkarnateTools.Inkarnate.Handlers;

internal sealed class GridEntityHandler : IInkEntityHandler
{
    public string EntityType => "grid";

    public TransactionUnderstanding Understanding => TransactionUnderstanding.FullyUnderstood;

    public void Apply(InkImportContext context, InkEntityItem item)
    {
        var entity = item.Entity;
        if (!entity.TryGetProperty("style", out var styleElement))
        {
            return;
        }

        var cellSize = InkJsonReader.ReadDouble(styleElement, "size");
        if (cellSize <= 0)
        {
            return;
        }

        context.Map.Grid.CellSize = cellSize;
    }
}

internal sealed class LightEntityHandler : IInkEntityHandler
{
    public string EntityType => "light";

    public TransactionUnderstanding Understanding => TransactionUnderstanding.FullyUnderstood;

    public void Apply(InkImportContext context, InkEntityItem item)
    {
        var entity = item.Entity;
        var light = new LightSource
        {
            Range = InkJsonReader.ReadDouble(entity, "range"),
            Color = EntityParsing.ReadColor(entity),
        };

        if (entity.TryGetProperty("position", out var positionElement))
        {
            light.Position = EntityParsing.ReadPoint(positionElement);
        }

        context.Map.Lights.Add(light);
    }
}

internal sealed class GroupEntityHandler : IInkEntityHandler
{
    public string EntityType => "group";

    public TransactionUnderstanding Understanding => TransactionUnderstanding.FullyUnderstood;

    public void Apply(InkImportContext context, InkEntityItem item)
    {
        var entity = item.Entity;
        var entityId = InkJsonReader.ReadInt(entity, "entityId");
        if (entityId is null or <= 0)
        {
            return;
        }

        if (!context.GroupsById.TryGetValue(entityId.Value, out var group))
        {
            group = new EntityGroup { GroupId = entityId.Value };
            context.GroupsById[entityId.Value] = group;
        }

        group.Name = InkJsonReader.ReadString(entity, "name")
            ?? InkJsonReader.ReadString(entity, "defaultName");
        group.LayerId = item.LayerId ?? group.LayerId;
        group.ParentGroupId = InkJsonReader.ReadInt(entity, "groupId");

        var originX = InkJsonReader.ReadDouble(entity, "x");
        var originY = InkJsonReader.ReadDouble(entity, "y");
        group.Origin = new MapPoint(originX, originY);
        group.RotationPivot = group.Origin;

        if (entity.TryGetProperty("angle", out var angleElement) &&
            angleElement.ValueKind == JsonValueKind.Number)
        {
            group.Angle = angleElement.GetDouble();
        }

        if (entity.TryGetProperty("oX", out var oxElement) && oxElement.ValueKind == JsonValueKind.Number &&
            entity.TryGetProperty("oY", out var oyElement) && oyElement.ValueKind == JsonValueKind.Number)
        {
            group.RotationPivot = new MapPoint(oxElement.GetDouble(), oyElement.GetDouble());
        }
    }
}

internal sealed class KnownIgnoredEntityHandler : IInkEntityHandler
{
    public KnownIgnoredEntityHandler(string entityType)
    {
        EntityType = entityType;
    }

    public string EntityType { get; }

    public TransactionUnderstanding Understanding => TransactionUnderstanding.KnownIgnored;

    public void Apply(InkImportContext context, InkEntityItem item)
    {
    }
}

internal static class EntityParsing
{
    public static MapPoint ReadPoint(JsonElement pointElement) =>
        new(InkJsonReader.ReadDouble(pointElement, "x"), InkJsonReader.ReadDouble(pointElement, "y"));

    public static void ApplyPortals(Wall wall, JsonElement portalsElement)
    {
        wall.Portals.Clear();
        if (portalsElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var portalElement in portalsElement.EnumerateArray())
        {
            var portal = new WallPortal
            {
                Id = InkJsonReader.ReadString(portalElement, "id") ?? string.Empty,
                Width = InkJsonReader.ReadDouble(portalElement, "width"),
            };

            if (portalElement.TryGetProperty("anchor", out var anchorElement))
            {
                portal.Anchor = ReadPoint(anchorElement);
            }

            wall.Portals.Add(portal);
        }
    }

    public static void ApplyPortalsIfPresent(Wall wall, JsonElement entityElement)
    {
        if (entityElement.TryGetProperty("portals", out var portalsElement))
        {
            ApplyPortals(wall, portalsElement);
        }
    }

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
