using Brickwork.Core.Models;
using Brickwork.Inkarnate;
using Xunit;

namespace Brickwork.Core.Tests;

public class SampleLightsImportTests
{
    private static string SampleLightsInkPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "test-maps", "brickwork-sample-lights.ink"));

    [Fact]
    public async Task ImportAsync_DetectsTorchFlickerPulseAndPlainLight()
    {
        Assert.True(File.Exists(SampleLightsInkPath), $"Missing test resource: {SampleLightsInkPath}");

        var map = await ImportAsync(new InkarnateImportOptions());

        Assert.Equal(4, map.Lights.Count);
        Assert.Empty(map.Walls);
        Assert.Empty(map.Regions);

        var lights = map.Lights.ToDictionary(light => light.EntityId);
        Assert.Equal(
            ["Candle Lit", "Red Glowing Crystal", "Torch Lit", "Warm Round Light"],
            lights.Values.Select(light => light.SourceName!).OrderBy(name => name, StringComparer.Ordinal).ToArray());

        AssertLight(lights[2], "Torch Lit", LightAnimation.Torch, 6025, 1504);
        AssertLight(lights[6], "Candle Lit", LightAnimation.Flicker, 2269, 1675);
        AssertLight(lights[8], "Red Glowing Crystal", LightAnimation.Pulse, 5971, 4142);
        AssertLight(lights[11], "Warm Round Light", LightAnimation.None, 2554, 4121);

        Assert.All(map.Lights, light =>
        {
            Assert.True(light.IsActive);
            Assert.Equal("layer-object-72", light.LayerId);
            Assert.Equal(LightSource.DefaultBrightRadius, light.BrightRadius);
            Assert.Equal(LightSource.DefaultDimRadius, light.DimRadius);
            Assert.Equal(light.SourceName, light.Name);
        });
    }

    [Fact]
    public async Task ImportAsync_DetectionDisabled_CreatesNoLights()
    {
        Assert.True(File.Exists(SampleLightsInkPath), $"Missing test resource: {SampleLightsInkPath}");

        var map = await ImportAsync(new InkarnateImportOptions { DetectLightsFromStamps = false });

        Assert.Empty(map.Lights);
    }

    [Fact]
    public async Task ImportAsync_AnimationDetectionDisabled_LeavesLightsStill()
    {
        Assert.True(File.Exists(SampleLightsInkPath), $"Missing test resource: {SampleLightsInkPath}");

        var map = await ImportAsync(new InkarnateImportOptions { DetectLightAnimations = false });

        Assert.Equal(4, map.Lights.Count);
        Assert.All(map.Lights, light => Assert.Equal(LightAnimation.None, light.Animation));
    }

    [Fact]
    public async Task ImportAsync_CustomAnimationWords_RemapPulseAndFlicker()
    {
        Assert.True(File.Exists(SampleLightsInkPath), $"Missing test resource: {SampleLightsInkPath}");

        var map = await ImportAsync(new InkarnateImportOptions
        {
            LightTorchWords = "candle",
            LightFlickerWords = "glowing",
            LightPulseWords = "warm",
        });

        var lights = map.Lights.ToDictionary(light => light.EntityId);
        Assert.Equal(LightAnimation.None, lights[2].Animation);
        Assert.Equal(LightAnimation.Torch, lights[6].Animation);
        Assert.Equal(LightAnimation.Flicker, lights[8].Animation);
        Assert.Equal(LightAnimation.Pulse, lights[11].Animation);
    }

    private static async Task<MapDocument> ImportAsync(InkarnateImportOptions options)
    {
        var importer = new InkarnateImporter { Options = options };
        await using var input = File.OpenRead(SampleLightsInkPath);
        return await importer.ImportAsync(input);
    }

    private static void AssertLight(
        LightSource light,
        string expectedName,
        LightAnimation expectedAnimation,
        double expectedX,
        double expectedY)
    {
        Assert.Equal(expectedName, light.SourceName);
        Assert.Equal(expectedAnimation, light.Animation);
        Assert.Equal(expectedX, light.Position.X, precision: 0);
        Assert.Equal(expectedY, light.Position.Y, precision: 0);
    }
}
