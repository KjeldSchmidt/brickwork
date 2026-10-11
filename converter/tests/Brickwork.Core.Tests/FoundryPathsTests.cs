using Brickwork.Core.Foundry;
using Xunit;

namespace Brickwork.Core.Tests;

public class FoundryPathsTests
{
    [Fact]
    public void BuildBackgroundSrc_WithoutMapsDirectory_ReturnsFileName()
    {
        Assert.Equal("basic-walls.webp", FoundryPaths.BuildBackgroundSrc(null, "basic-walls.ink"));
        Assert.Equal("map.webp", FoundryPaths.BuildBackgroundSrc("  ", null));
    }

    [Fact]
    public void BuildBackgroundSrc_UnderDataMaps_ReturnsDataRelativePath()
    {
        using var temp = new TempDirectory();
        var data = Path.Combine(temp.Path, "FoundryVTT", "Data");
        var maps = Path.Combine(data, "maps");
        Directory.CreateDirectory(maps);

        var src = FoundryPaths.BuildBackgroundSrc(maps, "dungeon.ink");

        Assert.Equal("maps/dungeon.webp", src);
    }

    [Fact]
    public void BuildBackgroundSrc_WhenJpgExists_UsesJpg()
    {
        using var temp = new TempDirectory();
        var data = Path.Combine(temp.Path, "FoundryVTT", "Data");
        var maps = Path.Combine(data, "maps");
        Directory.CreateDirectory(maps);
        File.WriteAllBytes(Path.Combine(maps, "dungeon.jpg"), [0xFF, 0xD8]);

        var src = FoundryPaths.BuildBackgroundSrc(maps, "dungeon.ink");

        Assert.Equal("maps/dungeon.jpg", src);
    }

    [Fact]
    public void BuildBackgroundSrc_WhenWebpAndJpgExist_PrefersWebp()
    {
        using var temp = new TempDirectory();
        var data = Path.Combine(temp.Path, "FoundryVTT", "Data");
        var maps = Path.Combine(data, "maps");
        Directory.CreateDirectory(maps);
        File.WriteAllBytes(Path.Combine(maps, "dungeon.jpg"), [0xFF, 0xD8]);
        File.WriteAllBytes(Path.Combine(maps, "dungeon.webp"), [0x52, 0x49]);

        var src = FoundryPaths.BuildBackgroundSrc(maps, "dungeon.ink");

        Assert.Equal("maps/dungeon.webp", src);
    }

    [Fact]
    public void BuildBackgroundSrc_WhenMapsIsDataRoot_ReturnsFileNameOnly()
    {
        using var temp = new TempDirectory();
        var data = Path.Combine(temp.Path, "FoundryVTT", "Data");
        Directory.CreateDirectory(data);

        var src = FoundryPaths.BuildBackgroundSrc(data, "scene.ink");

        Assert.Equal("scene.webp", src);
    }

    [Fact]
    public void BuildBackgroundSrc_UrlEscapesSpacesInPathSegments()
    {
        using var temp = new TempDirectory();
        var data = Path.Combine(temp.Path, "FoundryVTT", "Data");
        var maps = Path.Combine(data, "my maps");
        Directory.CreateDirectory(maps);
        File.WriteAllBytes(Path.Combine(maps, "old tavern.jpg"), [0xFF, 0xD8]);

        var src = FoundryPaths.BuildBackgroundSrc(maps, "old tavern.ink");

        Assert.Equal("my%20maps/old%20tavern.jpg", src);
    }

    [Fact]
    public void TryFindDataRoot_FindsAncestorNamedData()
    {
        using var temp = new TempDirectory();
        var nested = Path.Combine(temp.Path, "FoundryVTT", "Data", "worlds", "maps");
        Directory.CreateDirectory(nested);

        var root = FoundryPaths.TryFindDataRoot(nested);

        Assert.Equal(Path.Combine(temp.Path, "FoundryVTT", "Data"), root);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "brickwork-foundry-paths-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }
}
