using Brickwork.Core.Models;

namespace Brickwork.Core.Geometry;

/// <summary>
/// Works out which side of a closed wall is floor, the way a vector fill treats nested rings:
/// the outermost ring encloses floor, a ring inside it is a hole (rock or pillar), a ring inside
/// that encloses floor again, and so on.
/// </summary>
public static class WallNesting
{
    /// <summary>
    /// +1 when a positive offset should grow <paramref name="wall"/> outward to move it away from
    /// the floor, -1 when it should shrink inward (the wall is a hole), and +1 for open walls,
    /// whose side the user chooses with the sign.
    /// </summary>
    public static int AwayFromFloorSign(MapDocument map, Wall wall)
    {
        if (!wall.IsClosed)
        {
            return 1;
        }

        return Depth(map.Walls, wall) % 2 == 0 ? 1 : -1;
    }

    /// <summary>Number of other closed walls whose original path encloses <paramref name="wall"/>.</summary>
    public static int Depth(IEnumerable<Wall> walls, Wall wall)
    {
        var path = WallOffset.OriginalPath(wall);
        if (path.Count == 0)
        {
            return 0;
        }

        var probe = path[0];
        var depth = 0;
        foreach (var other in walls)
        {
            if (ReferenceEquals(other, wall) || !other.IsClosed)
            {
                continue;
            }

            var polygon = WallOffset.OriginalPath(other);
            if (polygon.Count >= 3 && Contains(polygon, probe))
            {
                depth++;
            }
        }

        return depth;
    }

    private static bool Contains(IList<MapPoint> polygon, MapPoint point)
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
