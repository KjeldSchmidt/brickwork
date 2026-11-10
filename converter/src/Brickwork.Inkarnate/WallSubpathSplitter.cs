using Brickwork.Core.Geometry;
using Brickwork.Core.Models;
using Brickwork.Inkarnate.Parsing;

namespace Brickwork.Inkarnate;

/// <summary>
/// Splits walls whose Inkarnate path holds several subpaths (e.g. a ring-shaped tunnel drawn as
/// an outer and an inner ellipse) into one wall per subpath, so no bridging segment is created
/// between them. The longest subpath keeps the original entity id and name.
/// </summary>
internal static class WallSubpathSplitter
{
    private const int MinimumSubpathPoints = 2;

    public static int SplitAll(InkImportContext context)
    {
        var nextEntityId = context.WallsByEntityId.Keys
            .Concat(context.GroupsById.Keys)
            .Concat(context.StampsByEntityId.Keys)
            .DefaultIfEmpty(0)
            .Max() + 1;
        var created = 0;

        foreach (var wall in context.WallsByEntityId.Values.OrderBy(wall => wall.EntityId).ToList())
        {
            var subpaths = SplitPoints(wall);
            if (subpaths.Count < 2)
            {
                continue;
            }

            var longestIndex = 0;
            for (var i = 1; i < subpaths.Count; i++)
            {
                if (WallPolylineEdges.TotalLength(subpaths[i], wall.IsClosed) >
                    WallPolylineEdges.TotalLength(subpaths[longestIndex], wall.IsClosed))
                {
                    longestIndex = i;
                }
            }

            var outer = subpaths[longestIndex];
            var parts = new List<Wall>();
            var partNumber = 1;
            for (var i = 0; i < subpaths.Count; i++)
            {
                if (i == longestIndex)
                {
                    continue;
                }

                partNumber++;
                var suffix = wall.IsClosed && IsInsidePolygon(subpaths[i][0], outer)
                    ? "inner"
                    : $"part {partNumber}";
                parts.Add(CreatePart(wall, nextEntityId++, subpaths[i], suffix));
            }

            var portals = wall.Portals.ToList();
            wall.Points.Clear();
            foreach (var point in outer)
            {
                wall.Points.Add(point);
            }

            AssignPortals(wall, parts, portals);

            foreach (var part in parts)
            {
                context.WallsByEntityId[part.EntityId] = part;
                if (context.PathWallTypes.TryGetValue(wall.EntityId, out var wallType))
                {
                    context.PathWallTypes[part.EntityId] = wallType;
                }

                if (part.GroupId is int groupId && context.GroupsById.TryGetValue(groupId, out var group))
                {
                    group.MemberIds.Add(part.EntityId);
                }

                created++;
            }
        }

        return created;
    }

    private static List<IList<MapPoint>> SplitPoints(Wall wall)
    {
        if (string.IsNullOrWhiteSpace(wall.PathData))
        {
            return [];
        }

        var counts = InkSvgPathParser.ParseSubpathPointCounts(wall.PathData);
        if (counts.Count < 2 || counts.Sum() != wall.Points.Count)
        {
            return [];
        }

        var subpaths = new List<IList<MapPoint>>();
        var offset = 0;
        foreach (var count in counts)
        {
            if (count >= MinimumSubpathPoints)
            {
                subpaths.Add(wall.Points.Skip(offset).Take(count).ToList());
            }

            offset += count;
        }

        return subpaths;
    }

    private static Wall CreatePart(Wall source, int entityId, IList<MapPoint> points, string suffix) =>
        new()
        {
            EntityId = entityId,
            Name = $"{source.DisplayName} ({suffix})",
            LayerId = source.LayerId,
            IsActive = source.IsActive,
            IsEntityVisible = source.IsEntityVisible,
            LineType = source.LineType,
            WallEnabled = source.WallEnabled,
            IsClosed = source.IsClosed,
            Origin = source.Origin,
            PathOrigin = source.PathOrigin,
            RotationPivot = source.RotationPivot,
            Angle = source.Angle,
            Scale = source.Scale,
            WallThickness = source.WallThickness,
            GroupId = source.GroupId,
            Points = points.ToList(),
        };

    /// <summary>Moves each portal onto whichever split wall runs closest to its anchor.</summary>
    private static void AssignPortals(Wall outer, IReadOnlyList<Wall> parts, IReadOnlyList<WallPortal> portals)
    {
        if (portals.Count == 0)
        {
            return;
        }

        outer.Portals.Clear();
        var candidates = new List<Wall> { outer };
        candidates.AddRange(parts);

        foreach (var portal in portals)
        {
            var scene = WallPathSegmentBuilder.PortalAnchorToScene(outer, portal);
            var target = candidates
                .OrderBy(candidate => DistanceSquared(WallGeometryEditing.SnapToCenterline(candidate, scene), scene))
                .First();
            target.Portals.Add(portal);
        }

        foreach (var candidate in candidates)
        {
            WallGeometryEditing.ResnapPortalAnchors(candidate);
        }
    }

    private static double DistanceSquared(MapPoint a, MapPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    private static bool IsInsidePolygon(MapPoint point, IList<MapPoint> polygon)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var a = polygon[i];
            var b = polygon[j];
            if ((a.Y > point.Y) != (b.Y > point.Y) &&
                point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
