using Brickwork.Core.Editing;
using Brickwork.Core.Geometry;
using Brickwork.Core.Models;
using Xunit;

namespace Brickwork.Core.Tests;

public class ProjectUpdateMergerTests
{
    [Fact]
    public void ChangesMadeInBrickwork_AreAppliedToTheNewBackup()
    {
        var project = Backup(wallY: 0);
        var wall = project.Walls.Single(w => w.EntityId == 1);
        wall.IsActive = false;
        wall.Levels = [2];
        wall.LineType = WallLineType.Window;
        wall.Portals[0].Width = 30;
        project.Lights[0].DimRadius = 55;
        project.SceneLevelCount = 3;

        // The backup moved the wall and renamed the light in Inkarnate.
        var newer = Backup(wallY: 50);
        newer.Lights[0].Name = "Brazier";

        var result = ProjectUpdateMerger.Merge(project, Backup(wallY: 0), newer).Map;

        var merged = result.Walls.Single(w => w.EntityId == 1);
        Assert.False(merged.IsActive);
        Assert.Equal([2], merged.Levels);
        Assert.Equal(WallLineType.Window, merged.LineType);
        Assert.Equal(50, merged.Points[0].Y);
        Assert.Equal(30, merged.Portals.Single().Width);
        Assert.Equal(50, merged.Portals.Single().Anchor.Y);
        Assert.Equal(55, result.Lights[0].DimRadius);
        Assert.Equal("Brazier", result.Lights[0].Name);
        Assert.Equal(3, result.SceneLevelCount);
    }

    [Fact]
    public void Names_FollowInkarnate_UnlessRenamedInBrickwork()
    {
        var project = Backup(wallY: 0);
        project.Walls.Single(w => w.EntityId == 2).Name = "Mine";
        project.Layers.Single(l => l.Id == "layer-a").Name = "Ground floor";

        var newer = Backup(wallY: 0);
        newer.Walls.Single(w => w.EntityId == 1).Name = "North wall";
        newer.Walls.Single(w => w.EntityId == 2).Name = "Theirs";
        newer.Layers.Single(l => l.Id == "layer-a").Name = "Renamed in Inkarnate";
        newer.Layers.Single(l => l.Id == "layer-b").Name = "Also renamed in Inkarnate";

        var result = ProjectUpdateMerger.Merge(project, Backup(wallY: 0), newer).Map;

        Assert.Equal("North wall", result.Walls.Single(w => w.EntityId == 1).Name);
        Assert.Equal("Mine", result.Walls.Single(w => w.EntityId == 2).Name);
        Assert.Equal("Ground floor", result.Layers.Single(l => l.Id == "layer-a").Name);
        Assert.Equal("Also renamed in Inkarnate", result.Layers.Single(l => l.Id == "layer-b").Name);
    }

    [Fact]
    public void AddedAndRemovedInInkarnate_FollowTheBackup_DeletedInBrickworkStaysDeleted()
    {
        var project = Backup(wallY: 0);
        project.Walls.Remove(project.Walls.Single(w => w.EntityId == 2));
        project.Groups[0].MemberIds.Remove(2);

        var newer = Backup(wallY: 0);
        newer.Walls.Remove(newer.Walls.Single(w => w.EntityId == 3));
        newer.Groups[0].MemberIds.Remove(3);
        newer.Layers.Add(new MapLayer { Id = "layer-new", Name = "Cellar", Order = 5 });
        newer.Walls.Add(Wall(10, "layer-new", y: 300));
        newer.Layers.Remove(newer.Layers.Single(l => l.Id == "layer-b"));

        var update = ProjectUpdateMerger.Merge(project, Backup(wallY: 0), newer);

        Assert.Equal([1, 10], update.Map.Walls.Select(w => w.EntityId).Order());
        Assert.Equal((1, 1), (update.Added, update.Removed));
        Assert.Equal(["layer-a", "layer-new"], update.Map.Layers.Select(l => l.Id).Order());
        Assert.Equal([1], update.Map.Groups[0].MemberIds);
    }

    [Fact]
    public void WallDrawnInBrickwork_IsKept_AndMovesAsideWhenItsIdIsTaken()
    {
        var project = Backup(wallY: 0);
        var drawnId = RegionConversion.AllocateEntityId(project);
        project.Walls.Add(Wall(drawnId, MapLayerEditing.DefaultLayerId, y: 400));
        MapLayerEditing.EnsureDefaultLayer(project);

        // Inkarnate gives its next new entity the same id.
        var newer = Backup(wallY: 0);
        newer.Walls.Add(Wall(drawnId, "layer-a", y: 500));

        var result = ProjectUpdateMerger.Merge(project, Backup(wallY: 0), newer).Map;

        Assert.Equal(500, result.Walls.Single(w => w.EntityId == drawnId).Points[0].Y);
        var drawn = result.Walls.Single(w => w.Points[0].Y == 400);
        Assert.NotEqual(drawnId, drawn.EntityId);
        Assert.Contains(result.Layers, l => l.Id == MapLayerEditing.DefaultLayerId);
    }

    [Fact]
    public void Offset_IsPutBackOnTheNewPath_AndAnEditedPathIsKept()
    {
        var project = Backup(wallY: 0);
        WallOffset.Apply(project.Walls.Single(w => w.EntityId == 1), 10);
        var edited = project.Walls.Single(w => w.EntityId == 2);
        edited.Points[0] = new MapPoint(-5, -5);

        var result = ProjectUpdateMerger.Merge(project, Backup(wallY: 0), Backup(wallY: 50)).Map;

        var offset = result.Walls.Single(w => w.EntityId == 1);
        Assert.Equal(10, offset.Offset);
        Assert.Equal(50, offset.OffsetBasePoints[0].Y);
        Assert.NotEqual(50, offset.Points[0].Y);
        Assert.Equal(new MapPoint(-5, -5), result.Walls.Single(w => w.EntityId == 2).Points[0]);
    }

    /// <summary>Three walls (1 with a portal) in a group, a light, on two layers; wall 1 at <paramref name="wallY"/>.</summary>
    private static MapDocument Backup(double wallY)
    {
        var map = new MapDocument
        {
            Layers =
            {
                new MapLayer { Id = "layer-a", Name = "A", Order = 0 },
                new MapLayer { Id = "layer-b", Name = "B", Order = 1 },
            },
            Groups = { new EntityGroup { GroupId = 100, Name = "House", LayerId = "layer-a", MemberIds = { 1, 2, 3 } } },
        };
        var first = Wall(1, "layer-a", wallY);
        first.Portals.Add(new WallPortal { Id = "door", Anchor = new MapPoint(50, wallY), Width = 20 });
        map.Walls.Add(first);
        map.Walls.Add(Wall(2, "layer-a", y: 100));
        map.Walls.Add(Wall(3, "layer-b", y: 200));
        foreach (var wall in map.Walls)
        {
            wall.GroupId = 100;
        }

        map.Lights.Add(new LightSource { EntityId = 50, Name = "Torch", LayerId = "layer-b", Position = new MapPoint(5, 5) });
        MapReimportMerger.RecordImportedIds(map);
        return map;
    }

    private static Wall Wall(int id, string layerId, double y) =>
        new()
        {
            EntityId = id,
            Name = $"Wall {id}",
            LayerId = layerId,
            Points = { new MapPoint(0, y), new MapPoint(100, y) },
        };
}
