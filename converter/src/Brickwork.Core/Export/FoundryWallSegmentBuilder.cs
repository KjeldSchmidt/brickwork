using Brickwork.Core.Geometry;
using Brickwork.Core.Models;

namespace Brickwork.Core.Export;

public sealed record FoundryWallSegment(
    int X0,
    int Y0,
    int X1,
    int Y1,
    int Sight,
    int Light,
    int Sound,
    int Move,
    int Door,
    int? ThresholdLight = null,
    int? ThresholdSight = null,
    int? ThresholdSound = null,
    bool ThresholdAttenuation = false);

public static class FoundryWallSegmentBuilder
{
    public static IReadOnlyList<FoundryWallSegment> BuildFromMap(MapDocument map)
    {
        var transform = SceneTransform.FromMap(map)
            ?? throw new InvalidOperationException("Map preview and scene dimensions are required for Foundry export.");

        var segments = new List<FoundryWallSegment>();

        foreach (var wall in map.Walls.Where(wall => wall.WallEnabled))
        {
            if (wall.IsActive && TryBuildTerrainPolygonSegments(wall, transform, segments))
            {
                continue;
            }

            foreach (var run in WallPathSegmentBuilder.BuildExportRuns(wall))
            {
                if (!wall.IsActive && !run.IsPortal)
                {
                    continue;
                }

                AddRunSegments(run, transform, segments);
            }
        }

        return segments;
    }

    public static IReadOnlyList<FoundryWallSegment> BuildFromWall(Wall wall, SceneTransform transform)
    {
        var segments = new List<FoundryWallSegment>();

        if (wall.IsActive && TryBuildTerrainPolygonSegments(wall, transform, segments))
        {
            return segments;
        }

        foreach (var run in WallPathSegmentBuilder.BuildExportRuns(wall))
        {
            if (!wall.IsActive && !run.IsPortal)
            {
                continue;
            }

            AddRunSegments(run, transform, segments);
        }

        return segments;
    }

    private static bool TryBuildTerrainPolygonSegments(
        Wall wall,
        SceneTransform transform,
        List<FoundryWallSegment> segments)
    {
        if (wall.LineType != WallLineType.Terrain ||
            wall.Points.Count < 2 ||
            wall.SceneThickness <= 0)
        {
            return false;
        }

        var pathSegments = WallPathSegmentBuilder.BuildSegments(wall);
        if (pathSegments.Count == 0)
        {
            return false;
        }

        var added = false;
        foreach (var pathSegment in pathSegments)
        {
            var loops = WallThicknessPolygonBuilder.BuildTerrainExportLoops(
                pathSegment.Points as IList<MapPoint> ?? pathSegment.Points.ToList(),
                wall.SceneThickness,
                pathSegment.IsClosed);

            foreach (var loop in loops)
            {
                AddLoopSegments(loop, WallLineType.Terrain, transform, segments);
                added = true;
            }
        }

        return added;
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

        var restrictions = FoundryWallRestrictions.ForLineType(lineType);
        segments.Add(new FoundryWallSegment(
            x0,
            y0,
            x1,
            y1,
            restrictions.Sight,
            restrictions.Light,
            restrictions.Sound,
            restrictions.Move,
            restrictions.Door,
            restrictions.ThresholdLight,
            restrictions.ThresholdSight,
            restrictions.ThresholdSound,
            restrictions.ThresholdAttenuation));
    }
}
