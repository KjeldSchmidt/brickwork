using Brickwork.Core.Editing;
using Brickwork.Core.Geometry;
using Brickwork.Core.Models;
using Xunit;

namespace Brickwork.Core.Tests;

public class ReimportAndNestingTests
{
    [Theory]
    [InlineData("Hand Lantern Lit", LightAnimation.Flicker)]
    [InlineData("Wall Torch", LightAnimation.Torch)]
    [InlineData("Lantern by the Fire", LightAnimation.Torch)]
    [InlineData("Glowing Crystals", LightAnimation.Pulse)]
    [InlineData("Lamp Post", LightAnimation.Flicker)]
    [InlineData("Light", LightAnimation.None)]
    public void Detect_PicksAnimationFromName(string name, LightAnimation expected)
    {
        Assert.Equal(expected, LightAnimations.Detect(name));
    }

    [Fact]
    public void AwayFromFloorSign_FlipsForRingsInsideOtherRings()
    {
        var outer = Square(1, 0, 100);
        var hole = Square(2, 40, 60);
        var pillarInHole = Square(3, 45, 55);
        var open = new Wall { EntityId = 4, Points = [new(0, 0), new(10, 0)] };
        var map = new MapDocument { Walls = [outer, hole, pillarInHole, open] };

        Assert.Equal(1, WallNesting.AwayFromFloorSign(map, outer));
        Assert.Equal(-1, WallNesting.AwayFromFloorSign(map, hole));
        Assert.Equal(1, WallNesting.AwayFromFloorSign(map, pillarInHole));
        Assert.Equal(1, WallNesting.AwayFromFloorSign(map, open));
    }

    [Fact]
    public void AwayFromFloorSign_UsesOriginalPathOfOffsetWalls()
    {
        var outer = Square(1, 0, 100);
        var inner = Square(2, 40, 60);
        var map = new MapDocument { Walls = [outer, inner] };

        // Shrinking the outer ring past the inner one must not change who is inside whom.
        WallOffset.Apply(outer, -45);

        Assert.Equal(-1, WallNesting.AwayFromFloorSign(map, inner));
    }

    [Fact]
    public void Merge_KeepsUserEditsAndTakesFreshGeometry()
    {
        var target = new MapDocument
        {
            Walls =
            [
                new Wall
                {
                    EntityId = 1,
                    Name = "Renamed",
                    LineType = WallLineType.Window,
                    Points = [new(0, 0), new(100, 0)],
                },
            ],
        };
        MapReimportMerger.RecordImportedIds(target);
        WallOffset.Apply(target.Walls[0], 10);
        var instance = target.Walls[0];

        var fresh = new MapDocument
        {
            Walls = [new Wall { EntityId = 1, Name = "Wall", Points = [new(0, 0), new(50, 0), new(100, 0)] }],
        };

        MapReimportMerger.Merge(target, fresh, wallsAndRegions: true, lights: false);

        var wall = Assert.Single(target.Walls);
        Assert.Same(instance, wall);
        Assert.Equal("Renamed", wall.Name);
        Assert.Equal(WallLineType.Window, wall.LineType);
        Assert.Equal(3, wall.Points.Count);
        Assert.Equal(10, wall.Offset);
        Assert.All(wall.Points, point => Assert.Equal(10, point.Y, precision: 6));
    }

    [Fact]
    public void Merge_RespectsDeletionsAndKeepsDrawnWalls()
    {
        var target = new MapDocument
        {
            Walls = [Square(1, 0, 10), Square(2, 20, 30)],
        };
        MapReimportMerger.RecordImportedIds(target);
        target.Walls.RemoveAt(1); // user deleted wall 2
        target.Walls.Add(Square(50, 40, 50)); // user drew wall 50

        var fresh = new MapDocument
        {
            Walls = [Square(1, 0, 10), Square(2, 20, 30), Square(7, 60, 70)],
        };

        var result = MapReimportMerger.Merge(target, fresh, wallsAndRegions: true, lights: false);

        Assert.Equal([1, 7, 50], target.Walls.Select(wall => wall.EntityId).Order().ToArray());
        Assert.Equal(1, result.Added);
    }

    [Fact]
    public void Merge_FollowsChangedRegionDetection()
    {
        var target = new MapDocument { Walls = [Square(1, 0, 10)] };
        MapReimportMerger.RecordImportedIds(target);
        target.Walls[0].IsActive = false;

        var fresh = new MapDocument
        {
            Regions = [new Region { EntityId = 1, Points = Square(1, 0, 10).Points.ToList() }],
        };

        MapReimportMerger.Merge(target, fresh, wallsAndRegions: true, lights: false);

        Assert.Empty(target.Walls);
        var region = Assert.Single(target.Regions);
        Assert.False(region.IsActive);
    }

    [Fact]
    public void Merge_Lights_AddsNewMatchesRemovesStaleAndKeepsSettings()
    {
        var target = new MapDocument
        {
            Lights =
            [
                new LightSource { EntityId = 10, DimRadius = 3 },
                new LightSource { EntityId = 11 },
            ],
        };
        MapReimportMerger.RecordImportedIds(target);

        var fresh = new MapDocument
        {
            Lights =
            [
                new LightSource { EntityId = 10, DimRadius = 8, Position = new MapPoint(5, 5) },
                new LightSource { EntityId = 12 },
            ],
        };

        var result = MapReimportMerger.Merge(target, fresh, wallsAndRegions: false, lights: true);

        Assert.Equal([10, 12], target.Lights.Select(light => light.EntityId).Order().ToArray());
        var kept = target.Lights.Single(light => light.EntityId == 10);
        Assert.Equal(3, kept.DimRadius);
        Assert.Equal(new MapPoint(5, 5), kept.Position);
        Assert.Equal(new MapReimportResult(1, 1, 1), result);
    }

    [Fact]
    public void Merge_IsUndoneByMemento()
    {
        var target = new MapDocument { Walls = [Square(1, 0, 10)] };
        MapReimportMerger.RecordImportedIds(target);
        var before = DocumentContentMemento.Capture(target);

        MapReimportMerger.Merge(
            target,
            new MapDocument { Walls = [Square(1, 0, 20), Square(2, 30, 40)] },
            wallsAndRegions: true,
            lights: false);
        before.RestoreTo(target);

        var wall = Assert.Single(target.Walls);
        Assert.Contains(new MapPoint(10, 10), wall.Points);
        Assert.Equal([1], target.ImportedEntityIds.ToArray());
    }

    private static Wall Square(int id, double min, double max) =>
        new()
        {
            EntityId = id,
            IsClosed = true,
            Points = [new(min, min), new(max, min), new(max, max), new(min, max)],
        };
}
