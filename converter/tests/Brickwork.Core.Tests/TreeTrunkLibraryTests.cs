using Brickwork.Core.Models;
using Brickwork.Core.Settings;
using Xunit;

namespace Brickwork.Core.Tests;

public class TreeTrunkLibraryTests
{
    private static Dictionary<string, TreeTrunkAdjustment> BuiltIn() =>
        new() { ["asset:1"] = new() { Name = "Palm Tree", Scale = 0.6, OffsetX = 0.1 } };

    [Fact]
    public void UserValues_KeepOnlyDifferencesFromBuiltIn()
    {
        var library = new TreeTrunkLibrary(
            BuiltIn(),
            new Dictionary<string, TreeTrunkAdjustment>
            {
                ["asset:1"] = new() { Scale = 0.6, OffsetX = 0.1 }, // same as built-in
                ["asset:2"] = new() { Scale = 1 }, // unadjusted, no built-in
                ["asset:3"] = new() { Scale = 2 },
            });

        Assert.Equal(["asset:3"], library.UserValues.Keys.ToArray());
        Assert.Equal(0.6, library.Get("asset:1").Scale);
    }

    [Fact]
    public void Set_OverridesBuiltInAndResetRestoresIt()
    {
        var library = new TreeTrunkLibrary(BuiltIn());

        library.Set("asset:1", new TreeTrunkAdjustment { Scale = 0.8, OffsetX = 0.1 });
        Assert.Equal(0.8, library.Effective["asset:1"].Scale);
        Assert.Single(library.UserValues);

        var reset = library.Reset("asset:1");
        Assert.Equal(0.6, reset.Scale);
        Assert.Empty(library.UserValues);
    }

    [Fact]
    public void Set_RoundsToThreeDecimals()
    {
        var library = new TreeTrunkLibrary();

        library.Set("asset:9", new TreeTrunkAdjustment { Scale = 0.6356646640104009, OffsetY = -0.15049 });

        Assert.Equal(0.636, library.UserValues["asset:9"].Scale);
        Assert.Equal(-0.15, library.UserValues["asset:9"].OffsetY);
    }

    [Fact]
    public void Serialize_IsPasteableLibraryFormat()
    {
        var json = TreeTrunkLibraryStore.Serialize(new Dictionary<string, TreeTrunkAdjustment>
        {
            ["asset:2"] = new() { Name = "Oak", Scale = 1.23456 },
            ["asset:1"] = new() { Name = "Palm Tree", Scale = 1 },
        });

        Assert.DoesNotContain("isDefault", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("isIdentity", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("asset:1", json, StringComparison.Ordinal);
        Assert.Contains("1.235", json, StringComparison.Ordinal);

        var roundTrip = TreeTrunkLibraryStore.Deserialize(json);
        Assert.Equal(1.235, Assert.Single(roundTrip).Value.Scale);
    }

    [Fact]
    public void LoadBuiltIn_ReadsShippedLibrary()
    {
        var builtIn = TreeTrunkLibraryStore.LoadBuiltIn();

        Assert.NotEmpty(builtIn);
        Assert.All(builtIn, entry => Assert.StartsWith("asset:", entry.Key, StringComparison.Ordinal));
    }
}
