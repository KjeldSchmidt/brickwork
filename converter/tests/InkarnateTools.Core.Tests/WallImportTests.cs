using InkarnateTools.Core.Models;
using InkarnateTools.Inkarnate;
using Xunit;

namespace InkarnateTools.Core.Tests;

public class WallImportTests
{
    private static string WallsSamplePath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "walls-sample.json"));

    [Fact]
    public async Task ImportAsync_ReadsWallPolylines_InSceneCoordinates()
    {
        Assert.True(File.Exists(WallsSamplePath), $"Missing test resource: {WallsSamplePath}");

        var importer = new InkarnateImporter();
        await using var input = File.OpenRead(WallsSamplePath);

        var map = await importer.ImportAsync(input);

        Assert.Equal(2, map.Walls.Count);

        var firstWall = map.Walls[0];
        Assert.Equal(3, firstWall.Points.Count);
        Assert.Equal(new MapPoint(100, 100), firstWall.Points[0]);
        Assert.Equal(new MapPoint(400, 100), firstWall.Points[1]);
        Assert.Equal(new MapPoint(400, 400), firstWall.Points[2]);

        var transform = SceneTransform.FromMap(map);
        Assert.NotNull(transform);

        var previewStart = transform!.SceneToPreview(firstWall.Points[0]);
        Assert.Equal(50, previewStart.X, precision: 3);
        Assert.Equal(50, previewStart.Y, precision: 3);

        Assert.NotNull(map.PreviewImagePng);
        Assert.DoesNotContain("bezier", File.ReadAllText(WallsSamplePath), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("curve", File.ReadAllText(WallsSamplePath), StringComparison.OrdinalIgnoreCase);
    }
}
