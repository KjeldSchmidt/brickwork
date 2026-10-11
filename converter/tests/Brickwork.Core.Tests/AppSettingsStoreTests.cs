using Brickwork.Core.Geometry;
using Brickwork.Core.Settings;
using Xunit;

namespace Brickwork.Core.Tests;

public class AppSettingsStoreTests
{
    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        using var temp = new TempDirectory();
        var store = new AppSettingsStore(temp.Path);

        var settings = store.Load();

        Assert.Equal(WallSimplificationSettings.DefaultToleranceSceneUnits, settings.WallSimplificationTolerance);
        Assert.False(settings.ShowDebugPanel);
        Assert.True(settings.DetectRegionsFromColorPaths);
        Assert.True(settings.DetectRegionsFromNamedContainers);
        Assert.True(settings.DetectRegionsFromPathNames);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsValues()
    {
        using var temp = new TempDirectory();
        var store = new AppSettingsStore(temp.Path);

        store.Save(new AppSettings
        {
            WallSimplificationTolerance = 42.5,
            ShowDebugPanel = true,
            FoundryMapsPath = @"C:\FoundryVTT\Data\maps",
            DetectRegionsFromColorPaths = false,
            DetectRegionsFromNamedContainers = false,
            DetectRegionsFromPathNames = true,
        });

        var loaded = store.Load();

        Assert.Equal(42.5, loaded.WallSimplificationTolerance);
        Assert.True(loaded.ShowDebugPanel);
        Assert.Equal(@"C:\FoundryVTT\Data\maps", loaded.FoundryMapsPath);
        Assert.False(loaded.DetectRegionsFromColorPaths);
        Assert.False(loaded.DetectRegionsFromNamedContainers);
        Assert.True(loaded.DetectRegionsFromPathNames);
        Assert.True(File.Exists(store.SettingsFilePath));
    }

    [Fact]
    public void Load_CorruptJson_ReturnsDefaults()
    {
        using var temp = new TempDirectory();
        var store = new AppSettingsStore(temp.Path);
        Directory.CreateDirectory(temp.Path);
        File.WriteAllText(store.SettingsFilePath, "{ not valid json");

        var settings = store.Load();

        Assert.Equal(WallSimplificationSettings.DefaultToleranceSceneUnits, settings.WallSimplificationTolerance);
        Assert.False(settings.ShowDebugPanel);
    }

    [Fact]
    public void Load_NegativeTolerance_ResetsToDefault()
    {
        using var temp = new TempDirectory();
        var store = new AppSettingsStore(temp.Path);
        Directory.CreateDirectory(temp.Path);
        File.WriteAllText(
            store.SettingsFilePath,
            """{"wallSimplificationTolerance":-5,"showDebugPanel":true}""");

        var settings = store.Load();

        Assert.Equal(WallSimplificationSettings.DefaultToleranceSceneUnits, settings.WallSimplificationTolerance);
        Assert.True(settings.ShowDebugPanel);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "brickwork-settings-tests-" + Guid.NewGuid().ToString("N"));

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
                // Best-effort cleanup for temp test dirs.
            }
        }
    }
}
