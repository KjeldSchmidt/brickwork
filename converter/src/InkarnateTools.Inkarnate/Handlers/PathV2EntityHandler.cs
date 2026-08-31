using System.Text.Json;
using InkarnateTools.Core.Geometry;
using InkarnateTools.Core.Models;
using InkarnateTools.Inkarnate.Parsing;

namespace InkarnateTools.Inkarnate.Handlers;

internal sealed class PathV2EntityHandler : IInkEntityHandler
{
    public string EntityType => "path-v2";

    public TransactionUnderstanding Understanding => TransactionUnderstanding.FullyUnderstood;

    public void Apply(InkImportContext context, InkEntityItem item)
    {
        var entity = item.Entity;
        if (!entity.TryGetProperty("wallEnabled", out var wallEnabledElement) ||
            wallEnabledElement.ValueKind != JsonValueKind.True)
        {
            return;
        }

        var entityId = InkJsonReader.ReadInt(entity, "entityId");
        if (entityId is null or <= 0)
        {
            return;
        }

        var pathData = InkJsonReader.ReadString(entity, "paths");
        if (string.IsNullOrWhiteSpace(pathData))
        {
            return;
        }

        var originX = InkJsonReader.ReadDouble(entity, "x");
        var originY = InkJsonReader.ReadDouble(entity, "y");
        var scale = InkJsonReader.ReadDouble(entity, "scale");
        if (scale <= 0)
        {
            scale = 1;
        }

        var isClosed = entity.TryGetProperty("isClosedPath", out var closedElement) &&
                       closedElement.ValueKind == JsonValueKind.True;

        var rawPoints = InkSvgPathParser.ParseToScenePoints(pathData, originX, originY, scale);
        if (rawPoints.Count < 2)
        {
            return;
        }

        var wall = new Wall
        {
            EntityId = entityId.Value,
            Name = InkJsonReader.ReadString(entity, "defaultName"),
            LayerId = item.LayerId,
            WallEnabled = true,
            IsClosed = isClosed,
            PathData = pathData,
            Origin = new MapPoint(originX, originY),
            Scale = scale,
            WallThickness = InkJsonReader.ReadDouble(entity, "wallThickness"),
        };

        foreach (var point in rawPoints)
        {
            wall.RawPoints.Add(point);
        }

        WallPointSimplifier.Apply(wall, WallSimplificationSettings.DefaultToleranceSceneUnits);

        context.WallsByEntityId[wall.EntityId] = wall;
    }
}
