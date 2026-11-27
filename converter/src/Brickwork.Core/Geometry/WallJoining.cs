using Brickwork.Core.Models;

namespace Brickwork.Core.Geometry;

public sealed record WallJoinOptions(
    double EndpointTolerance = 1.5,
    bool PromoteDoorSegmentsToPortals = true,
    bool JoinAcrossLayers = false);

/// <summary>
/// Joins open wall polylines that share endpoints (same layer and line type),
/// closes loops, and optionally promotes Door/SecretDoor segments into portals
/// on adjacent body walls.
/// </summary>
public static class WallJoining
{
    private static readonly WallLineType[] PortalSegmentTypes =
    [
        WallLineType.Door,
        WallLineType.SecretDoor,
    ];

    public static IReadOnlyList<Wall> Join(
        IEnumerable<Wall> walls,
        WallJoinOptions? options = null,
        int startingEntityId = 1)
    {
        ArgumentNullException.ThrowIfNull(walls);
        options ??= new WallJoinOptions();

        var source = walls.Where(wall => wall.Points.Count >= 2).ToList();
        if (source.Count == 0)
        {
            return [];
        }

        var portalCandidates = new List<Wall>();
        var bodyWalls = new List<Wall>();

        foreach (var wall in source)
        {
            if (options.PromoteDoorSegmentsToPortals &&
                PortalSegmentTypes.Contains(wall.LineType) &&
                wall.Portals.Count == 0)
            {
                portalCandidates.Add(wall);
            }
            else
            {
                bodyWalls.Add(wall);
            }
        }

        var joinedBodies = JoinBodyWalls(bodyWalls, options);
        var leftoverPortals = new List<Wall>();

        if (options.PromoteDoorSegmentsToPortals)
        {
            foreach (var door in portalCandidates)
            {
                if (!TryPromoteDoorToPortal(joinedBodies, door, options.EndpointTolerance))
                {
                    leftoverPortals.Add(door);
                }
            }
        }

        // Bridging doors can merge body walls; re-run a join pass on the updated set.
        joinedBodies = JoinBodyWalls(joinedBodies, options);

        var joinedDoors = JoinBodyWalls(leftoverPortals, options);
        var result = new List<Wall>(joinedBodies.Count + joinedDoors.Count);
        result.AddRange(joinedBodies);
        result.AddRange(joinedDoors);

        return Renumber(result, startingEntityId);
    }

    private static List<Wall> JoinBodyWalls(IReadOnlyList<Wall> walls, WallJoinOptions options)
    {
        if (walls.Count == 0)
        {
            return [];
        }

        var groups = walls.GroupBy(wall =>
            options.JoinAcrossLayers
                ? (LayerId: string.Empty, wall.LineType)
                : (LayerId: wall.LayerId ?? string.Empty, wall.LineType));

        var joined = new List<Wall>();
        foreach (var group in groups)
        {
            joined.AddRange(JoinGroup(group.ToList(), options.EndpointTolerance, group.Key.LineType));
        }

        return joined;
    }

    private static List<Wall> JoinGroup(
        IReadOnlyList<Wall> walls,
        double tolerance,
        WallLineType lineType)
    {
        var remaining = new List<Wall>();
        var result = new List<Wall>();
        var toleranceSquared = tolerance * tolerance;

        // Already-closed walls are final; re-joining would treat corners as open ends.
        foreach (var wall in walls)
        {
            if (wall.IsClosed && wall.Points.Count >= 3)
            {
                result.Add(CloneWall(wall));
            }
            else
            {
                remaining.Add(wall);
            }
        }

        while (remaining.Count > 0)
        {
            var chain = new List<MapPoint>(remaining[0].Points);
            var layerId = remaining[0].LayerId;
            var carriedPortals = new List<(WallPortal Portal, MapPoint SceneAnchor)>();
            CarryPortals(remaining[0], carriedPortals);
            remaining.RemoveAt(0);

            var grew = true;
            while (grew)
            {
                grew = false;
                for (var i = 0; i < remaining.Count; i++)
                {
                    var candidate = remaining[i];
                    if (!TryAppend(chain, candidate.Points, toleranceSquared, out var appended))
                    {
                        continue;
                    }

                    chain = appended;
                    CarryPortals(candidate, carriedPortals);
                    remaining.RemoveAt(i);
                    grew = true;
                    break;
                }
            }

            var isClosed = chain.Count >= 3 &&
                           DistanceSquared(chain[0], chain[^1]) <= toleranceSquared;
            var cleaned = WallPolylineCleaner.DeduplicateClosePoints(
                chain,
                minDistance: Math.Max(WallPolylineCleaner.DefaultMinPointSpacing, tolerance * 0.25),
                closeLoop: isClosed);

            if (cleaned.Count < 2)
            {
                continue;
            }

            // Closed loops: drop duplicate closing vertex if cleaner left it.
            if (isClosed &&
                cleaned.Count >= 3 &&
                DistanceSquared(cleaned[0], cleaned[^1]) <= toleranceSquared)
            {
                cleaned.RemoveAt(cleaned.Count - 1);
            }

            isClosed = isClosed && cleaned.Count >= 3;

            var wall = CreateWall(cleaned, lineType, layerId, isClosed);
            AttachCarriedPortals(wall, carriedPortals);
            result.Add(wall);
        }

        return result;
    }

    private static bool TryAppend(
        IList<MapPoint> chain,
        IList<MapPoint> candidate,
        double toleranceSquared,
        out List<MapPoint> appended)
    {
        appended = [];
        if (candidate.Count < 2 || chain.Count < 2)
        {
            return false;
        }

        var head = chain[0];
        var tail = chain[^1];
        var candHead = candidate[0];
        var candTail = candidate[^1];

        if (DistanceSquared(tail, candHead) <= toleranceSquared)
        {
            appended = new List<MapPoint>(chain);
            appended.AddRange(candidate.Skip(1));
            return true;
        }

        if (DistanceSquared(tail, candTail) <= toleranceSquared)
        {
            appended = new List<MapPoint>(chain);
            for (var i = candidate.Count - 2; i >= 0; i--)
            {
                appended.Add(candidate[i]);
            }

            return true;
        }

        if (DistanceSquared(head, candTail) <= toleranceSquared)
        {
            appended = new List<MapPoint>(candidate);
            appended.AddRange(chain.Skip(1));
            return true;
        }

        if (DistanceSquared(head, candHead) <= toleranceSquared)
        {
            appended = new List<MapPoint>(candidate.Count + chain.Count - 1);
            for (var i = candidate.Count - 1; i >= 0; i--)
            {
                appended.Add(candidate[i]);
            }

            appended.AddRange(chain.Skip(1));
            return true;
        }

        return false;
    }

    private static void CarryPortals(Wall wall, List<(WallPortal Portal, MapPoint SceneAnchor)> carried)
    {
        foreach (var portal in wall.Portals)
        {
            if (portal.Width <= 0)
            {
                continue;
            }

            carried.Add((ClonePortal(portal), WallPathSegmentBuilder.PortalAnchorToScene(wall, portal)));
        }
    }

    private static void AttachCarriedPortals(
        Wall wall,
        IReadOnlyList<(WallPortal Portal, MapPoint SceneAnchor)> carried)
    {
        foreach (var (portal, sceneAnchor) in carried)
        {
            portal.Anchor = MapPointTransforms.SceneToLocal(wall, sceneAnchor);
            wall.Portals.Add(portal);
        }

        if (wall.Portals.Count > 0)
        {
            WallGeometryEditing.ResnapPortalAnchors(wall);
        }
    }

    private static bool TryPromoteDoorToPortal(
        List<Wall> bodyWalls,
        Wall door,
        double tolerance)
    {
        if (door.Points.Count < 2)
        {
            return false;
        }

        var a = door.Points[0];
        var b = door.Points[^1];
        var width = Distance(a, b);
        if (width <= tolerance)
        {
            return false;
        }

        var mid = new MapPoint((a.X + b.X) * 0.5d, (a.Y + b.Y) * 0.5d);
        var toleranceSquared = tolerance * tolerance;

        // Prefer a single host that already contains both door ends on its centerline.
        for (var i = 0; i < bodyWalls.Count; i++)
        {
            var body = bodyWalls[i];
            if (!IsNearCenterline(body, a, tolerance) || !IsNearCenterline(body, b, tolerance))
            {
                continue;
            }

            return AttachPortal(body, mid, width, door);
        }

        // Otherwise bridge two open body walls whose free endpoints match the door ends.
        var hostsA = FindEndpointHosts(bodyWalls, a, toleranceSquared);
        var hostsB = FindEndpointHosts(bodyWalls, b, toleranceSquared);
        foreach (var (indexA, atStartA) in hostsA)
        {
            foreach (var (indexB, atStartB) in hostsB)
            {
                if (indexA == indexB)
                {
                    continue;
                }

                var left = bodyWalls[indexA];
                var right = bodyWalls[indexB];
                if (!string.Equals(left.LayerId, right.LayerId, StringComparison.Ordinal) ||
                    left.LineType != right.LineType ||
                    left.IsClosed ||
                    right.IsClosed)
                {
                    continue;
                }

                var mergedPoints = BridgeThroughDoor(
                    left.Points,
                    atStartA,
                    right.Points,
                    atStartB,
                    a,
                    b);
                if (mergedPoints is null)
                {
                    continue;
                }

                var carried = new List<(WallPortal Portal, MapPoint SceneAnchor)>();
                CarryPortals(left, carried);
                CarryPortals(right, carried);

                var cleaned = WallPolylineCleaner.DeduplicateClosePoints(
                    mergedPoints,
                    minDistance: Math.Max(WallPolylineCleaner.DefaultMinPointSpacing, tolerance * 0.25));
                if (cleaned.Count < 2)
                {
                    continue;
                }

                var merged = CreateWall(cleaned, left.LineType, left.LayerId, isClosed: false);
                AttachCarriedPortals(merged, carried);
                if (!AttachPortal(merged, mid, width, door))
                {
                    continue;
                }

                // Remove higher index first.
                var removeFirst = Math.Max(indexA, indexB);
                var removeSecond = Math.Min(indexA, indexB);
                bodyWalls.RemoveAt(removeFirst);
                bodyWalls.RemoveAt(removeSecond);
                bodyWalls.Add(merged);
                return true;
            }
        }

        return false;
    }

    private static bool AttachPortal(Wall body, MapPoint mid, double width, Wall door)
    {
        var portal = WallGeometryEditing.TryAddPortal(body, mid, width);
        if (portal is null)
        {
            return false;
        }

        portal.LineType = door.LineType;
        portal.Name = door.Name;
        portal.IsActive = door.IsActive;
        return true;
    }

    private static List<(int Index, bool AtStart)> FindEndpointHosts(
        IReadOnlyList<Wall> bodyWalls,
        MapPoint point,
        double toleranceSquared)
    {
        var hosts = new List<(int Index, bool AtStart)>();
        for (var i = 0; i < bodyWalls.Count; i++)
        {
            var wall = bodyWalls[i];
            if (wall.IsClosed || wall.Points.Count < 2)
            {
                continue;
            }

            if (DistanceSquared(wall.Points[0], point) <= toleranceSquared)
            {
                hosts.Add((i, AtStart: true));
            }
            else if (DistanceSquared(wall.Points[^1], point) <= toleranceSquared)
            {
                hosts.Add((i, AtStart: false));
            }
        }

        return hosts;
    }

    private static List<MapPoint>? BridgeThroughDoor(
        IList<MapPoint> left,
        bool doorAtLeftStart,
        IList<MapPoint> right,
        bool doorAtRightStart,
        MapPoint doorA,
        MapPoint doorB)
    {
        // Orient left so the door attachment is at its end.
        var leftPoints = doorAtLeftStart
            ? left.Reverse().ToList()
            : left.ToList();

        // Orient right so the door attachment is at its start.
        var rightPoints = doorAtRightStart
            ? right.ToList()
            : right.Reverse().ToList();

        // Ensure door endpoints align: left end should match one door end, right start the other.
        if (DistanceSquared(leftPoints[^1], doorA) > DistanceSquared(leftPoints[^1], doorB))
        {
            (doorA, doorB) = (doorB, doorA);
        }

        if (DistanceSquared(leftPoints[^1], doorA) > 1e-6 ||
            DistanceSquared(rightPoints[0], doorB) > 1e-6)
        {
            // Soft check — still concatenate; door mid is implied by the gap endpoints.
        }

        var merged = new List<MapPoint>(leftPoints.Count + rightPoints.Count);
        merged.AddRange(leftPoints);
        merged.AddRange(rightPoints.Skip(1));
        return merged;
    }

    private static bool IsNearCenterline(Wall wall, MapPoint point, double tolerance)
    {
        var toleranceSquared = tolerance * tolerance;
        foreach (var (start, end, _) in WallPolylineEdges.EnumerateEdges(wall.Points, wall.IsClosed))
        {
            if (DistanceSquaredToSegment(point, start, end) <= toleranceSquared)
            {
                return true;
            }
        }

        return false;
    }

    private static Wall CreateWall(
        IReadOnlyList<MapPoint> points,
        WallLineType lineType,
        string? layerId,
        bool isClosed)
    {
        var origin = points[0];
        return new Wall
        {
            EntityId = 0,
            Name = "Wall",
            LayerId = layerId,
            LineType = lineType,
            IsClosed = isClosed,
            Origin = origin,
            PathOrigin = origin,
            RotationPivot = origin,
            Points = points.ToList(),
        };
    }

    private static IReadOnlyList<Wall> Renumber(IReadOnlyList<Wall> walls, int startingEntityId)
    {
        var entityId = Math.Max(1, startingEntityId);
        var result = new List<Wall>(walls.Count);
        foreach (var wall in walls)
        {
            var id = entityId++;
            result.Add(new Wall
            {
                EntityId = id,
                Name = string.IsNullOrWhiteSpace(wall.Name) || wall.Name == "Wall"
                    ? $"Wall {id}"
                    : wall.Name,
                LayerId = wall.LayerId,
                IsActive = wall.IsActive,
                IsEntityVisible = wall.IsEntityVisible,
                LineType = wall.LineType,
                WallEnabled = wall.WallEnabled,
                IsClosed = wall.IsClosed,
                PathData = wall.PathData,
                Origin = wall.Origin,
                PathOrigin = wall.PathOrigin,
                RotationPivot = wall.RotationPivot,
                Angle = wall.Angle,
                Scale = wall.Scale,
                WallThickness = wall.WallThickness,
                GroupId = wall.GroupId,
                Points = wall.Points.ToList(),
                Portals = wall.Portals.ToList(),
            });
        }

        return result;
    }

    private static Wall CloneWall(Wall wall) =>
        new()
        {
            EntityId = wall.EntityId,
            Name = wall.Name,
            LayerId = wall.LayerId,
            IsActive = wall.IsActive,
            IsEntityVisible = wall.IsEntityVisible,
            LineType = wall.LineType,
            WallEnabled = wall.WallEnabled,
            IsClosed = wall.IsClosed,
            PathData = wall.PathData,
            Origin = wall.Origin,
            PathOrigin = wall.PathOrigin,
            RotationPivot = wall.RotationPivot,
            Angle = wall.Angle,
            Scale = wall.Scale,
            WallThickness = wall.WallThickness,
            GroupId = wall.GroupId,
            Points = wall.Points.ToList(),
            Portals = wall.Portals.Select(ClonePortal).ToList(),
        };

    private static WallPortal ClonePortal(WallPortal portal) =>
        new()
        {
            Id = string.IsNullOrWhiteSpace(portal.Id) ? Guid.NewGuid().ToString("N") : portal.Id,
            Name = portal.Name,
            Anchor = portal.Anchor,
            Width = portal.Width,
            IsActive = portal.IsActive,
            LineType = portal.LineType,
        };

    private static double Distance(MapPoint left, MapPoint right) =>
        Math.Sqrt(DistanceSquared(left, right));

    private static double DistanceSquared(MapPoint left, MapPoint right)
    {
        var dx = left.X - right.X;
        var dy = left.Y - right.Y;
        return (dx * dx) + (dy * dy);
    }

    private static double DistanceSquaredToSegment(MapPoint point, MapPoint start, MapPoint end)
    {
        var abx = end.X - start.X;
        var aby = end.Y - start.Y;
        var lengthSquared = (abx * abx) + (aby * aby);
        if (lengthSquared <= 1e-18)
        {
            return DistanceSquared(point, start);
        }

        var t = ((point.X - start.X) * abx + (point.Y - start.Y) * aby) / lengthSquared;
        t = Math.Clamp(t, 0d, 1d);
        var proj = new MapPoint(start.X + (t * abx), start.Y + (t * aby));
        return DistanceSquared(point, proj);
    }
}
