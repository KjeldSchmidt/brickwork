using System.Text;
using Brickwork.Core.Editing;
using Brickwork.Core.Models;
using Brickwork.Inkarnate;
using Xunit;

namespace Brickwork.Core.Tests;

public class TreeTrunkTests
{
    private static readonly IReadOnlyList<string> Include = NameKeywords.Parse(TreeTrunks.DefaultInclude);
    private static readonly IReadOnlyList<string> Exclude = NameKeywords.Parse(TreeTrunks.DefaultExclude);

    [Theory]
    [InlineData("Palm Tree", true)]
    [InlineData("Large Oak", true)]
    [InlineData("Pine Trees", true)]
    [InlineData("Fallen Tree", false)]
    [InlineData("Tree Stump", false)]
    [InlineData("Berry Bush", false)]
    [InlineData("Train Chair Firstclass", false)]
    [InlineData("Ashtray", false)]
    public void Matches_TreeNames(string name, bool expected)
    {
        Assert.Equal(expected, NameKeywords.Matches(name, Include, Exclude));
    }

    [Fact]
    public void BuildX_CrossesAtCenterWithRequestedLength()
    {
        var arms = TreeTrunks.BuildX(new MapPoint(100, 100), 20, 0);

        Assert.Equal(2, arms.Count);
        foreach (var (start, end) in arms)
        {
            Assert.Equal(100, (start.X + end.X) / 2, precision: 6);
            Assert.Equal(100, (start.Y + end.Y) / 2, precision: 6);
            Assert.Equal(20, Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2)), precision: 6);
        }

        Assert.Equal(new MapPoint(100 - Math.Sqrt(50), 100 - Math.Sqrt(50)).X, arms[0].Start.X, precision: 6);
    }

    [Fact]
    public async Task Import_TreeStamps_GetGroupedTrunkWalls()
    {
        var map = await ImportAsync(new InkarnateImportOptions());

        var trunks = map.Walls.Where(wall => wall.EntityId < 0).ToList();
        Assert.Equal(8, trunks.Count); // 4 trees x 2 arms; bush and fallen tree skipped
        Assert.All(trunks, wall => Assert.Equal(WallLineType.Solid, wall.LineType));
        Assert.All(trunks, wall => Assert.Equal(2, wall.Points.Count));
        Assert.All(trunks, wall => Assert.True(TreeTrunks.IsTrunk(wall)));
        Assert.Equal(10, map.Walls.Single(wall => wall.EntityId == -20).Trunk!.StampId);

        var restored = new MapDocument();
        DocumentContentMemento.Capture(map).RestoreTo(restored);
        Assert.Equal(10, restored.Walls.Single(wall => wall.EntityId == -20).Trunk!.StampId);

        var palm = trunks.Where(wall => wall.EntityId is -20 or -21).ToList();
        Assert.Equal(2, palm.Count);
        Assert.All(palm, wall => Assert.Equal(25, Length(wall), precision: 6)); // 0.5 squares of 50
        Assert.All(palm, wall => Assert.Equal(new MapPoint(200, 300), Middle(wall)));

        var palmGroup = Assert.Single(map.Groups, group => group.Name == "Palm Tree trunks");
        Assert.Equal("layer-trees", palmGroup.LayerId);
        Assert.Equal(6, palmGroup.MemberIds.Count);

        // Palm scales are 1, 2 and 1: the median (1) gets the configured size; the palm twice as large
        // gets a 1.5x trunk at the default 50 % growth.
        Assert.All(trunks.Where(wall => wall.EntityId is -22 or -23), wall => Assert.Equal(37.5, Length(wall), precision: 6));

        // A tree on a hidden layer gets inactive trunk walls.
        Assert.All(trunks.Where(wall => wall.LayerId == "layer-hidden"), wall => Assert.False(wall.IsActive));
    }

    [Fact]
    public async Task Import_RecordsTreeVersionFromStampAsset()
    {
        var map = await ImportAsync(new InkarnateImportOptions());

        Assert.Equal(501, map.Walls.Single(wall => wall.EntityId == -20).Trunk!.AssetId);
        Assert.Equal(502, map.Walls.Single(wall => wall.EntityId == -22).Trunk!.AssetId);
    }

    [Theory]
    [InlineData(2, 50, 1.5)]
    [InlineData(0.5, 50, 0.75)]
    [InlineData(2, 0, 1)]
    [InlineData(2, 100, 2)]
    [InlineData(1, 50, 1)]
    public void SizeFactor_FollowsShareOfTreeGrowth(double sizeRatio, double growthPercent, double expected)
    {
        Assert.Equal(expected, TreeTrunks.SizeFactor(sizeRatio, growthPercent), precision: 9);
    }

    [Fact]
    public async Task Import_GrowthOnlyDampsSize_OffsetsFollowFullTreeSize()
    {
        var adjustments = new Dictionary<string, TreeTrunkAdjustment>
        {
            [TreeTrunks.VersionKey(502, "Palm Tree")] = new() { OffsetY = 0.5 },
        };

        var map = await ImportAsync(new InkarnateImportOptions { TreeAdjustments = adjustments });

        // The 2x palm (stamp 11, rotated 30 degrees): offset 0.5 squares of 2 x 50, turned with the stamp.
        var arm = map.Walls.Single(wall => wall.EntityId == -22);
        var expected = Brickwork.Core.Geometry.MapPointTransforms.RotateAround(
            new MapPoint(400, 350), new MapPoint(400, 300), 30);
        Assert.Equal(Math.Round(expected.X, 6), Middle(arm).X);
        Assert.Equal(Math.Round(expected.Y, 6), Middle(arm).Y);
        Assert.Equal(37.5, Length(arm), precision: 6);
    }

    [Fact]
    public void Translate_MovesStoredTreeCentreSoAdjustKeepsTheMove()
    {
        var wall = new Wall { Trunk = new TreeTrunk(1, 7, 0, new MapPoint(100, 100), 0, 10, 10, 2), Points = [] };
        TreeTrunks.Adjust(wall, 1, 0, 0);

        Brickwork.Core.Geometry.WallGeometryEditing.Translate(wall, 30, -20);
        TreeTrunks.Adjust(wall, 2, 0, 0);

        Assert.Equal(new MapPoint(130, 80), Middle(wall));
        Assert.Equal(40, Length(wall), precision: 6);
    }

    [Fact]
    public void Adjust_ScalesAndOffsetsInTheStampsRotatedFrame()
    {
        var trunk = new TreeTrunk(1, 7, 0, new MapPoint(100, 100), Angle: 90, Unit: 10, SizeUnit: 10, BaseSize: 2);
        var wall = new Wall { Trunk = trunk, Points = [] };

        TreeTrunks.Adjust(wall, scale: 1.5, offsetX: 1, offsetY: 0);

        // "Right" in the image is "down" on the map after a 90 degree turn.
        Assert.Equal(new MapPoint(100, 110), Middle(wall));
        Assert.Equal(30, Length(wall), precision: 6);
        Assert.Equal(1.5, wall.Trunk!.Scale);
    }

    [Fact]
    public async Task Import_AppliesLibraryAdjustmentPerVersion()
    {
        var adjustments = new Dictionary<string, TreeTrunkAdjustment>
        {
            [TreeTrunks.VersionKey(501, "Palm Tree")] = new() { Scale = 2, OffsetX = 0.5 },
        };

        var map = await ImportAsync(new InkarnateImportOptions { TreeAdjustments = adjustments });

        var adjusted = map.Walls.Single(wall => wall.EntityId == -20);
        Assert.Equal(50, Length(adjusted), precision: 6);
        Assert.Equal(new MapPoint(225, 300), Middle(adjusted)); // 0.5 squares of 50 to the right

        var otherVersion = map.Walls.Single(wall => wall.EntityId == -22);
        Assert.Equal(1, otherVersion.Trunk!.Scale);
    }

    [Fact]
    public async Task Reimport_TakesAdjustmentsAndNewSizeFromFreshImport()
    {
        var adjustments = new Dictionary<string, TreeTrunkAdjustment>
        {
            [TreeTrunks.VersionKey(501, "Palm Tree")] = new() { Scale = 2 },
        };
        var map = await ImportAsync(new InkarnateImportOptions { TreeAdjustments = adjustments });
        MapReimportMerger.RecordImportedIds(map);
        var palmArm = map.Walls.Single(wall => wall.EntityId == -20);

        var bigger = await ImportAsync(new InkarnateImportOptions { TreeTrunkSize = 1, TreeAdjustments = adjustments });
        MapReimportMerger.Merge(map, bigger, wallsAndRegions: true, lights: false);

        Assert.Equal(2, palmArm.Trunk!.Scale);
        Assert.Equal(100, Length(palmArm), precision: 6); // 1 square of 50, doubled
        Assert.Equal(new MapPoint(200, 300), Middle(palmArm));
    }

    [Fact]
    public void LibraryStore_RoundTripsAndSkipsDefaults()
    {
        var directory = Path.Combine(Path.GetTempPath(), "brickwork-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new Brickwork.Core.Settings.TreeTrunkLibraryStore(directory);
            store.Save(new Dictionary<string, TreeTrunkAdjustment>
            {
                ["asset:1"] = new() { Name = "Palm Tree", Scale = 1.5, OffsetY = -0.25 },
                ["asset:2"] = new() { Name = "Oak" },
            });

            var loaded = store.Load();

            var palm = Assert.Single(loaded);
            Assert.Equal("asset:1", palm.Key);
            Assert.Equal(1.5, palm.Value.Scale);
            Assert.Equal(-0.25, palm.Value.OffsetY);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Import_ScalingDisabled_GivesEveryTrunkTheSameSize()
    {
        var map = await ImportAsync(new InkarnateImportOptions { TreeTrunkGrowthPercent = 0 });

        Assert.All(map.Walls.Where(wall => wall.EntityId < 0), wall => Assert.Equal(25, Length(wall), precision: 6));
    }

    [Fact]
    public async Task Import_TreeDetectionDisabled_AddsNoTrunks()
    {
        var map = await ImportAsync(new InkarnateImportOptions { DetectTreesFromStamps = false });

        Assert.DoesNotContain(map.Walls, wall => wall.EntityId < 0);
        Assert.Empty(map.Groups);
    }

    [Fact]
    public async Task Reimport_EnablingTrees_AddsTrunksAndKeepsDisabledOnes()
    {
        var map = await ImportAsync(new InkarnateImportOptions { DetectTreesFromStamps = false });
        MapReimportMerger.RecordImportedIds(map);

        var withTrees = await ImportAsync(new InkarnateImportOptions());
        MapReimportMerger.Merge(map, withTrees, wallsAndRegions: true, lights: false);
        Assert.Equal(8, map.Walls.Count(wall => wall.EntityId < 0));
        Assert.Equal(2, map.Groups.Count);

        map.Walls.Single(wall => wall.EntityId == -20).IsActive = false;
        var bigger = await ImportAsync(new InkarnateImportOptions { TreeTrunkSize = 1 });
        MapReimportMerger.Merge(map, bigger, wallsAndRegions: true, lights: false);

        var palmArm = map.Walls.Single(wall => wall.EntityId == -20);
        Assert.False(palmArm.IsActive);
        Assert.Equal(50, Length(palmArm), precision: 6);
    }

    private static double Length(Wall wall) =>
        Math.Sqrt(Math.Pow(wall.Points[1].X - wall.Points[0].X, 2) + Math.Pow(wall.Points[1].Y - wall.Points[0].Y, 2));

    private static MapPoint Middle(Wall wall) =>
        new(
            Math.Round((wall.Points[0].X + wall.Points[1].X) / 2, 6),
            Math.Round((wall.Points[0].Y + wall.Points[1].Y) / 2, 6));

    private static async Task<MapDocument> ImportAsync(InkarnateImportOptions options)
    {
        const string json = """
            {
              "title": "trees",
              "version": 3,
              "scene": { "normSceneSize": { "w": 1000, "h": 1000 } },
              "history": [
                { "transactionId": 1, "cmdType": "cmd-layer-add", "layerId": "layer-trees", "layerKind": "entity",
                  "layerData": { "name": "Trees", "isVisible": true } },
                { "transactionId": 2, "cmdType": "cmd-layer-add", "layerId": "layer-hidden", "layerKind": "entity",
                  "layerData": { "name": "Hidden", "isVisible": false } },
                { "transactionId": 3, "cmdType": "cmd-entity-add", "items": [
                  { "layerId": "layer-grid", "entity": { "entityType": "grid", "entityId": 1, "style": { "size": 50 } } },
                  { "layerId": "layer-trees", "entity": { "entityType": "stamp", "entityId": 10, "defaultName": "Palm Tree", "stampId": 501, "x": 200, "y": 300, "scale": 1 } },
                  { "layerId": "layer-trees", "entity": { "entityType": "stamp", "entityId": 11, "defaultName": "Palm Tree", "stampId": 502, "x": 400, "y": 300, "angle": 30, "scale": 2 } },
                  { "layerId": "layer-trees", "entity": { "entityType": "stamp", "entityId": 12, "defaultName": "Berry Bush", "x": 1, "y": 1 } },
                  { "layerId": "layer-trees", "entity": { "entityType": "stamp", "entityId": 13, "defaultName": "Fallen Tree", "x": 2, "y": 2 } },
                  { "layerId": "layer-hidden", "entity": { "entityType": "stamp", "entityId": 14, "defaultName": "Oak", "x": 3, "y": 3 } },
                  { "layerId": "layer-trees", "entity": { "entityType": "stamp", "entityId": 15, "defaultName": "Palm Tree", "x": 600, "y": 300, "scale": 1 } }
                ] }
              ]
            }
            """;

        var importer = new InkarnateImporter { Options = options };
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return await importer.ImportAsync(input);
    }
}
