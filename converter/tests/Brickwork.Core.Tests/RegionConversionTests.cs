using Brickwork.Core.Models;
using Xunit;

namespace Brickwork.Core.Tests;

public class RegionConversionTests
{
    [Fact]
    public void WallToRegion_ClosedWall_CopiesCenterline()
    {
        var wall = new Wall
        {
            EntityId = 1,
            Name = "Loop",
            IsClosed = true,
            LayerId = "layer-a",
            Points =
            {
                new MapPoint(0, 0),
                new MapPoint(10, 0),
                new MapPoint(10, 10),
                new MapPoint(0, 10),
            },
        };

        var region = RegionConversion.WallToRegion(wall, entityId: 7);

        Assert.Equal(7, region.EntityId);
        Assert.Equal("Loop", region.Name);
        Assert.Equal("layer-a", region.LayerId);
        Assert.Equal(wall.Points.ToList(), region.Points.ToList());
    }

    [Fact]
    public void WallToRegion_OpenWall_UsesTerrainOutline()
    {
        var wall = new Wall
        {
            EntityId = 1,
            IsClosed = false,
            WallThickness = 10,
            Scale = 1,
            Points =
            {
                new MapPoint(0, 0),
                new MapPoint(100, 0),
            },
        };

        var region = RegionConversion.WallToRegion(wall, entityId: 2);

        Assert.True(region.Points.Count >= 3);
        // Outline should expand off the centerline (not just the two endpoints).
        Assert.Contains(region.Points, point => Math.Abs(point.Y) > 1e-6);
    }

    [Fact]
    public void WallToRegion_OpenWallZeroThickness_UsesDefaultTerrainThickness()
    {
        var wall = new Wall
        {
            EntityId = 1,
            IsClosed = false,
            WallThickness = 0,
            Scale = 1,
            Points =
            {
                new MapPoint(0, 0),
                new MapPoint(100, 0),
            },
        };

        var region = RegionConversion.WallToRegion(wall, entityId: 2);

        Assert.True(region.Points.Count >= 3);
        var halfDefault = WallLineEditing.DefaultTerrainWallThickness / 2d;
        Assert.Contains(region.Points, point => Math.Abs(Math.Abs(point.Y) - halfDefault) < 1e-6);
    }

    [Fact]
    public void RegionToWall_CreatesClosedSolidWall()
    {
        var region = new Region
        {
            EntityId = 3,
            Name = "Patch",
            LayerId = "L",
            RegionType = RegionType.DifficultTerrain,
            Points =
            {
                new MapPoint(0, 0),
                new MapPoint(5, 0),
                new MapPoint(5, 5),
            },
        };

        var wall = RegionConversion.RegionToWall(region, entityId: 9);

        Assert.Equal(9, wall.EntityId);
        Assert.Equal("Patch", wall.Name);
        Assert.True(wall.IsClosed);
        Assert.Equal(WallLineType.Solid, wall.LineType);
        Assert.Empty(wall.Portals);
        Assert.Equal(region.Points.ToList(), wall.Points.ToList());
    }

    [Fact]
    public void TryConvertWallToRegion_ReplacesWallOnMap()
    {
        var map = new MapDocument();
        var wall = new Wall
        {
            EntityId = 1,
            IsClosed = true,
            Points =
            {
                new MapPoint(0, 0),
                new MapPoint(10, 0),
                new MapPoint(10, 10),
            },
        };
        map.Walls.Add(wall);

        Assert.True(RegionConversion.TryConvertWallToRegion(map, wall, out var region));
        Assert.NotNull(region);
        Assert.Empty(map.Walls);
        Assert.Same(region, map.Regions.Single());
    }

    [Fact]
    public void TryConvertRegionToWall_ReplacesRegionOnMap()
    {
        var map = new MapDocument();
        var region = new Region
        {
            EntityId = 1,
            Points =
            {
                new MapPoint(0, 0),
                new MapPoint(10, 0),
                new MapPoint(10, 10),
            },
        };
        map.Regions.Add(region);

        Assert.True(RegionConversion.TryConvertRegionToWall(map, region, out var wall));
        Assert.NotNull(wall);
        Assert.Empty(map.Regions);
        Assert.Same(wall, map.Walls.Single());
        Assert.True(wall.IsClosed);
    }
}
