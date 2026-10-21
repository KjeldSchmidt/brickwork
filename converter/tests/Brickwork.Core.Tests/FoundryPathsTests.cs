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

    [Fact]
    public void ResolveBackgroundBaseName_InkUsesMapTitle()
    {
        Assert.Equal("Cool Dungeon", FoundryPaths.ResolveBackgroundBaseName("backup-123.ink", "Cool Dungeon"));
        Assert.Equal("backup-123", FoundryPaths.ResolveBackgroundBaseName("backup-123.ink", null));
        Assert.Equal("scene", FoundryPaths.ResolveBackgroundBaseName("scene.uvtt", "Ignored Title"));
    }

    [Fact]
    public void BuildBackgroundSrc_InkUsesMapTitle()
    {
        using var temp = new TempDirectory();
        var data = Path.Combine(temp.Path, "FoundryVTT", "Data");
        var maps = Path.Combine(data, "maps");
        Directory.CreateDirectory(maps);
        File.WriteAllBytes(Path.Combine(maps, "Cool Dungeon.webp"), [0x52, 0x49]);

        var src = FoundryPaths.BuildBackgroundSrc(maps, "offline-backup.ink", "Cool Dungeon");

        Assert.Equal("maps/Cool%20Dungeon.webp", src);
    }

    [Fact]
    public void TryResolveBackgroundImagePath_WhenMissing_ReturnsNull()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.Path);

        Assert.Null(FoundryPaths.TryResolveBackgroundImagePath(temp.Path, "missing.ink"));
        Assert.Null(FoundryPaths.TryResolveBackgroundImagePath(null, "dungeon.ink"));
    }

    [Fact]
    public void BuildBackgroundSrc_ImageInSubfolder_IncludesSubfolder()
    {
        using var temp = new TempDirectory();
        var data = Path.Combine(temp.Path, "FoundryVTT", "Data");
        var maps = Path.Combine(data, "Uploads", "Maps", "Inkarnate");
        var oskar = Path.Combine(maps, "Oskar");
        Directory.CreateDirectory(oskar);
        File.WriteAllBytes(Path.Combine(oskar, "Hochhorst Ebnen.webp"), [0x52, 0x49]);

        var src = FoundryPaths.BuildBackgroundSrc(maps, "backup.ink", "Hochhorst Ebnen");

        Assert.Equal("Uploads/Maps/Inkarnate/Oskar/Hochhorst%20Ebnen.webp", src);
    }

    [Fact]
    public void TryResolveBackgroundImagePath_PrefersShallowestMatch()
    {
        using var temp = new TempDirectory();
        var deep = Path.Combine(temp.Path, "a", "b");
        Directory.CreateDirectory(deep);
        File.WriteAllBytes(Path.Combine(deep, "dungeon.webp"), [0x52, 0x49]);
        var shallow = Path.Combine(temp.Path, "z", "dungeon.png");
        Directory.CreateDirectory(Path.GetDirectoryName(shallow)!);
        File.WriteAllBytes(shallow, [0x89, 0x50]);

        var resolved = FoundryPaths.TryResolveBackgroundImagePath(temp.Path, "dungeon.ink");

        Assert.Equal(Path.GetFullPath(shallow), resolved);
    }

    [Fact]
    public void TryResolveBackgroundImagePath_WhenPresent_ReturnsAbsolutePath()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.Path);
        var imagePath = Path.Combine(temp.Path, "dungeon.png");
        File.WriteAllBytes(imagePath, [0x89, 0x50]);

        var resolved = FoundryPaths.TryResolveBackgroundImagePath(temp.Path, "dungeon.ink");

        Assert.Equal(Path.GetFullPath(imagePath), resolved);
    }

    [Fact]
    public void TryResolveBackgroundImagePath_InkUsesMapTitle()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.Path);
        var imagePath = Path.Combine(temp.Path, "Cool Dungeon.png");
        File.WriteAllBytes(imagePath, [0x89, 0x50]);

        var resolved = FoundryPaths.TryResolveBackgroundImagePath(
            temp.Path,
            "offline-backup.ink",
            "Cool Dungeon");

        Assert.Equal(Path.GetFullPath(imagePath), resolved);
        Assert.Null(FoundryPaths.TryResolveBackgroundImagePath(temp.Path, "offline-backup.ink", "Other"));
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
