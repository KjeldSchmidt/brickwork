using InkarnateTools.Inkarnate;
using Xunit;

namespace InkarnateTools.Core.Tests;

public class MassEditsImportTests
{
    private static string MapPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "map-with-mass-edits.ink"));

    [Fact]
    public async Task ImportAsync_DoesNotKeepReplacedPrisonBars()
    {
        Assert.True(File.Exists(MapPath), $"Missing test resource: {MapPath}");
        await using var input = File.OpenRead(MapPath);
        var map = await new InkarnateImporter().ImportAsync(input);

        var prisonBars = map.Walls
            .Where(wall => (wall.Name ?? string.Empty).Contains("Prison Bars", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.InRange(prisonBars.Count, 80, 88);
        Assert.DoesNotContain(prisonBars, wall => wall.EntityId == 9977);
        Assert.Contains(prisonBars, wall => wall.EntityId == 12263);
        Assert.DoesNotContain(prisonBars, wall => wall.EntityId == 11090);
        Assert.Contains(prisonBars, wall => wall.EntityId == 12223);
    }
}
