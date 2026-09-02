using Brickwork.Core.Export;
using Brickwork.Core.Geometry;
using Brickwork.Core.Models;
using Brickwork.Inkarnate;
using Xunit;

namespace Brickwork.Core.Tests;

public class TerrainEllipseExportTests
{
    private static string MapPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "map-with-mass-edits.ink"));

    [Theory]
    [InlineData(1646)]
    [InlineData(2905)]
    [InlineData(2951)]
    public async Task ExportAsync_ClosedTerrainEllipse_ProducesGapAwareTerrainPolygons(int entityId)
    {
        await using var input = File.OpenRead(MapPath);
        var map = await new InkarnateImporter().ImportAsync(input);
        var wall = map.Walls.Single(candidate => candidate.EntityId == entityId);
        wall.LineType = WallLineType.Terrain;

        var transform = SceneTransform.FromMap(map)!;
        var segments = FoundryWallSegmentBuilder.BuildFromWall(wall, transform);
        var expectedSegmentCount = CountExpectedTerrainSegments(wall, transform);

        Assert.Equal(expectedSegmentCount, segments.Count);
        Assert.All(segments, segment => Assert.Equal(10, segment.Sight));

        if (entityId is 2905 or 2951)
        {
            Assert.True(wall.HasPortals());
            Assert.True(WallPathSegmentBuilder.BuildSegments(wall).Count > 1);
        }
        else
        {
            Assert.False(wall.HasPortals());
            Assert.Single(WallPathSegmentBuilder.BuildSegments(wall));
        }
    }

    private static int CountExpectedTerrainSegments(Wall wall, SceneTransform transform)
    {
        var count = 0;
        foreach (var pathSegment in WallPathSegmentBuilder.BuildSegments(wall))
        {
            var loops = WallThicknessPolygonBuilder.BuildTerrainExportLoops(
                pathSegment.Points as IList<MapPoint> ?? pathSegment.Points.ToList(),
                wall.SceneThickness,
                pathSegment.IsClosed);

            foreach (var loop in loops)
            {
                foreach (var (start, end, _) in WallPolylineEdges.EnumerateEdges(
                             loop as IList<MapPoint> ?? loop.ToList(),
                             isClosed: true))
                {
                    var previewStart = transform.SceneToPreview(start);
                    var previewEnd = transform.SceneToPreview(end);
                    var x0 = (int)Math.Round(previewStart.X);
                    var y0 = (int)Math.Round(previewStart.Y);
                    var x1 = (int)Math.Round(previewEnd.X);
                    var y1 = (int)Math.Round(previewEnd.Y);
                    if (x0 != x1 || y0 != y1)
                    {
                        count++;
                    }
                }
            }
        }

        return count;
    }
}
