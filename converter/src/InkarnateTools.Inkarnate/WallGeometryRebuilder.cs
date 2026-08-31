using InkarnateTools.Core.Geometry;
using InkarnateTools.Core.Models;
using InkarnateTools.Inkarnate.Parsing;

namespace InkarnateTools.Inkarnate;

internal static class WallGeometryRebuilder
{
    public static void RebuildFromPath(Wall wall)
    {
        if (string.IsNullOrWhiteSpace(wall.PathData))
        {
            return;
        }

        var points = InkSvgPathParser.ParseToScenePoints(
            wall.PathData,
            wall.PathOrigin.X,
            wall.PathOrigin.Y,
            wall.Scale,
            wall.Angle,
            wall.RotationPivot.X,
            wall.RotationPivot.Y);

        wall.RawPoints.Clear();
        foreach (var point in points)
        {
            wall.RawPoints.Add(point);
        }

        WallPointSimplifier.Apply(wall, WallSimplificationSettings.DefaultToleranceSceneUnits);
    }

    public static void ApplyEntityTransform(
        Wall wall,
        double? x,
        double? y,
        double? angle,
        double? originX,
        double? originY,
        double? scale)
    {
        var needsRebuild = false;

        if (scale is > 0 && Math.Abs(scale.Value - wall.Scale) > 1e-9)
        {
            wall.Scale = scale.Value;
            needsRebuild = true;
        }

        if (angle is not null)
        {
            if (Math.Abs(angle.Value - wall.Angle) > 1e-9)
            {
                needsRebuild = true;
            }

            wall.Angle = angle.Value;
            if (originX is not null && originY is not null)
            {
                wall.RotationPivot = new MapPoint(originX.Value, originY.Value);
            }
        }

        if (x is not null && y is not null)
        {
            var newOrigin = new MapPoint(x.Value, y.Value);
            if (Math.Abs(newOrigin.X - wall.PathOrigin.X) > 1e-9 ||
                Math.Abs(newOrigin.Y - wall.PathOrigin.Y) > 1e-9)
            {
                needsRebuild = true;
            }

            // Pure translates also keep the stored rotation pivot aligned with the entity.
            if (angle is null)
            {
                var dx = newOrigin.X - wall.Origin.X;
                var dy = newOrigin.Y - wall.Origin.Y;
                wall.RotationPivot = new MapPoint(wall.RotationPivot.X + dx, wall.RotationPivot.Y + dy);
            }

            wall.Origin = newOrigin;
            wall.PathOrigin = newOrigin;
        }

        if (needsRebuild)
        {
            RebuildFromPath(wall);
        }
    }

    public static void ApplyGroupTransform(
        InkImportContext context,
        EntityGroup group,
        double? x,
        double? y,
        double? angle,
        double? originX,
        double? originY)
    {
        var rotated = false;
        if (angle is not null && originX is not null && originY is not null)
        {
            var pivot = new MapPoint(originX.Value, originY.Value);
            var delta = angle.Value - group.Angle;
            if (Math.Abs(delta) > 1e-9)
            {
                ApplyRotationDeltaToGroupTree(context, group, pivot, delta);
            }

            group.Angle = angle.Value;
            group.RotationPivot = pivot;
            rotated = true;
        }

        if (x is not null && y is not null)
        {
            var newOrigin = new MapPoint(x.Value, y.Value);
            if (!rotated)
            {
                var dx = newOrigin.X - group.Origin.X;
                var dy = newOrigin.Y - group.Origin.Y;
                if (Math.Abs(dx) > 1e-9 || Math.Abs(dy) > 1e-9)
                {
                    ApplyTranslationDeltaToGroupTree(context, group, dx, dy);
                }
            }

            group.Origin = newOrigin;
        }
    }

    private static void ApplyRotationDeltaToGroupTree(
        InkImportContext context,
        EntityGroup group,
        MapPoint pivot,
        double deltaDegrees)
    {
        group.Origin = MapPointTransforms.RotateAround(group.Origin, pivot, deltaDegrees);

        foreach (var memberId in group.MemberIds)
        {
            if (context.WallsByEntityId.TryGetValue(memberId, out var wall))
            {
                MapPointTransforms.RotateAll(wall.RawPoints, pivot, deltaDegrees);
                MapPointTransforms.RotateAll(wall.Points, pivot, deltaDegrees);
                wall.Origin = MapPointTransforms.RotateAround(wall.Origin, pivot, deltaDegrees);
                wall.PathOrigin = MapPointTransforms.RotateAround(wall.PathOrigin, pivot, deltaDegrees);
                wall.RotationPivot = MapPointTransforms.RotateAround(wall.RotationPivot, pivot, deltaDegrees);
                wall.Angle += deltaDegrees;
                WallPointSimplifier.Apply(wall, WallSimplificationSettings.DefaultToleranceSceneUnits);
            }
            else if (context.GroupsById.TryGetValue(memberId, out var childGroup))
            {
                childGroup.Angle += deltaDegrees;
                childGroup.RotationPivot = MapPointTransforms.RotateAround(childGroup.RotationPivot, pivot, deltaDegrees);
                ApplyRotationDeltaToGroupTree(context, childGroup, pivot, deltaDegrees);
            }
        }
    }

    private static void ApplyTranslationDeltaToGroupTree(
        InkImportContext context,
        EntityGroup group,
        double dx,
        double dy)
    {
        group.RotationPivot = new MapPoint(group.RotationPivot.X + dx, group.RotationPivot.Y + dy);

        foreach (var memberId in group.MemberIds)
        {
            if (context.WallsByEntityId.TryGetValue(memberId, out var wall))
            {
                MapPointTransforms.Translate(wall.RawPoints, dx, dy);
                MapPointTransforms.Translate(wall.Points, dx, dy);
                wall.PathOrigin = new MapPoint(wall.PathOrigin.X + dx, wall.PathOrigin.Y + dy);
                wall.RotationPivot = new MapPoint(wall.RotationPivot.X + dx, wall.RotationPivot.Y + dy);
                wall.Origin = new MapPoint(wall.Origin.X + dx, wall.Origin.Y + dy);
            }
            else if (context.GroupsById.TryGetValue(memberId, out var childGroup))
            {
                childGroup.Origin = new MapPoint(childGroup.Origin.X + dx, childGroup.Origin.Y + dy);
                ApplyTranslationDeltaToGroupTree(context, childGroup, dx, dy);
            }
        }
    }
}
