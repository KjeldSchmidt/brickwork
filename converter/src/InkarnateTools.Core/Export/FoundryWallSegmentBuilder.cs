using InkarnateTools.Core.Geometry;
using InkarnateTools.Core.Models;

namespace InkarnateTools.Core.Export;

public sealed record FoundryWallSegment(
    int X0,
    int Y0,
    int X1,
    int Y1,
    int Sight,
    int Light,
    int Sound,
    int Move,
    int Door);

public static class FoundryWallSegmentBuilder
{
    public static IReadOnlyList<FoundryWallSegment> BuildFromMap(MapDocument map)
    {
        var transform = SceneTransform.FromMap(map)
            ?? throw new InvalidOperationException("Map preview and scene dimensions are required for Foundry export.");

        var segments = new List<FoundryWallSegment>();

        foreach (var wall in map.ExportableWalls())
        {
            if (TryBuildTerrainRingSegments(wall, transform, segments))
            {
                continue;
            }

            foreach (var run in WallPathSegmentBuilder.BuildExportRuns(wall))
            {
                AddRunSegments(run, transform, segments);
            }
        }

        return segments;
    }

    public static IReadOnlyList<FoundryWallSegment> BuildFromWall(Wall wall, SceneTransform transform)
    {
        var segments = new List<FoundryWallSegment>();

        if (TryBuildTerrainRingSegments(wall, transform, segments))
        {
            return segments;
        }

        foreach (var run in WallPathSegmentBuilder.BuildExportRuns(wall))
        {
            AddRunSegments(run, transform, segments);
        }

        return segments;
    }

    private static bool TryBuildTerrainRingSegments(
        Wall wall,
        SceneTransform transform,
        List<FoundryWallSegment> segments)
    {
        if (wall.LineType != WallLineType.Terrain ||
            !wall.IsClosed ||
            wall.SceneThickness <= 0 ||
            wall.Portals.Count > 0)
        {
            return false;
        }

        var ring = WallThicknessPolygonBuilder.BuildClosedRing(wall.Points, wall.SceneThickness);
        if (ring is null || ring.Outer.Count < 3 || ring.Inner.Count < 3)
        {
            return false;
        }

        AddLoopSegments(ring.Outer, WallLineType.Terrain, transform, segments);
        AddLoopSegments(ring.Inner, WallLineType.Terrain, transform, segments);
        return true;
    }

    private static void AddLoopSegments(
        IReadOnlyList<MapPoint> loop,
        WallLineType lineType,
        SceneTransform transform,
        List<FoundryWallSegment> segments)
    {
        foreach (var (start, end, _) in WallPolylineEdges.EnumerateEdges(
                     loop as IList<MapPoint> ?? loop.ToList(),
                     isClosed: true))
        {
            AddLineSegment(start, end, lineType, transform, segments);
        }
    }

    private static void AddRunSegments(
        WallExportRun run,
        SceneTransform transform,
        List<FoundryWallSegment> segments)
    {
        for (var i = 1; i < run.Points.Count; i++)
        {
            AddLineSegment(run.Points[i - 1], run.Points[i], run.LineType, transform, segments);
        }
    }

    private static void AddLineSegment(
        MapPoint start,
        MapPoint end,
        WallLineType lineType,
        SceneTransform transform,
        List<FoundryWallSegment> segments)
    {
        var previewStart = transform.SceneToPreview(start);
        var previewEnd = transform.SceneToPreview(end);

        var x0 = (int)Math.Round(previewStart.X);
        var y0 = (int)Math.Round(previewStart.Y);
        var x1 = (int)Math.Round(previewEnd.X);
        var y1 = (int)Math.Round(previewEnd.Y);

        if (x0 == x1 && y0 == y1)
        {
            return;
        }

        var (sight, door) = MapLineType(lineType);
        segments.Add(new FoundryWallSegment(x0, y0, x1, y1, sight, sight, sight, 20, door));
    }

    private static (int Sight, int Door) MapLineType(WallLineType lineType) =>
        lineType switch
        {
            WallLineType.Terrain => (10, 0),
            WallLineType.Door => (20, 1),
            _ => (20, 0),
        };
}
