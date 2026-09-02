using System.Text.Json;
using Brickwork.Core.Geometry;
using Brickwork.Core.Models;
using Brickwork.Inkarnate.Parsing;

namespace Brickwork.Inkarnate.Handlers;

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

        var isClosed = InkSvgPathParser.ResolveIsClosedPath(entity, pathData);

        var origin = new MapPoint(originX, originY);
        var wall = new Wall
        {
            EntityId = entityId.Value,
            Name = InkJsonReader.ReadString(entity, "defaultName"),
            LayerId = item.LayerId,
            WallEnabled = true,
            IsClosed = isClosed,
            PathData = pathData,
            Origin = origin,
            PathOrigin = origin,
            RotationPivot = origin,
            Scale = scale,
            WallThickness = InkJsonReader.ReadDouble(entity, "wallThickness"),
        };

        var angle = entity.TryGetProperty("angle", out var angleElement) &&
                    angleElement.ValueKind == JsonValueKind.Number
            ? angleElement.GetDouble()
            : 0d;
        if (Math.Abs(angle) > 1e-9)
        {
            wall.Angle = angle;
            if (entity.TryGetProperty("oX", out var oxElement) && oxElement.ValueKind == JsonValueKind.Number &&
                entity.TryGetProperty("oY", out var oyElement) && oyElement.ValueKind == JsonValueKind.Number)
            {
                wall.RotationPivot = new MapPoint(oxElement.GetDouble(), oyElement.GetDouble());
            }
        }

        var rawPoints = InkSvgPathParser.ParseToScenePoints(
            pathData,
            wall.PathOrigin.X,
            wall.PathOrigin.Y,
            wall.Scale,
            wall.Angle,
            wall.RotationPivot.X,
            wall.RotationPivot.Y);
        if (rawPoints.Count < 2)
        {
            return;
        }

        foreach (var point in rawPoints)
        {
            wall.RawPoints.Add(point);
        }

        WallPointSimplifier.Apply(wall, WallSimplificationSettings.DefaultToleranceSceneUnits);

        EntityParsing.ApplyPortalsIfPresent(wall, entity);

        context.WallsByEntityId[wall.EntityId] = wall;
        var groupId = InkJsonReader.ReadInt(entity, "groupId");
        if (groupId is > 0)
        {
            wall.GroupId = groupId.Value;
        }

        context.ApplyVisibilityToWall(wall, item.LayerId);
    }
}
