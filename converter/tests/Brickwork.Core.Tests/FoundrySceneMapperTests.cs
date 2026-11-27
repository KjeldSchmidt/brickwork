using Brickwork.Core.Models;
using Brickwork.Exporters.Foundry;
using Xunit;

namespace Brickwork.Core.Tests;

public class FoundrySceneMapperTests
{
    private static string WallsFixturePath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "test-maps", "fvtt-scene-basic-walls.json"));

    private static string RegionsFixturePath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "test-maps", "fvtt-basic-regions.json"));

    [Fact]
    public void MapFromJson_JoinedCollinearWall_DropsRedundantMidpoints()
    {
        const string json = """
            {
              "name": "collinear",
              "width": 1000,
              "height": 1000,
              "padding": 0,
              "grid": { "size": 100 },
              "walls": [
                { "_id": "a", "c": [0, 100, 200, 100], "move": 20, "sight": 20, "light": 20, "sound": 20 },
                { "_id": "b", "c": [200, 100, 400, 100], "move": 20, "sight": 20, "light": 20, "sound": 20 },
                { "_id": "c", "c": [400, 100, 600, 100], "move": 20, "sight": 20, "light": 20, "sound": 20 }
              ]
            }
            """;

        var map = FoundrySceneMapper.MapFromJson(json);

        var wall = Assert.Single(map.Walls);
        Assert.Equal(2, wall.Points.Count);
        Assert.Equal(new MapPoint(0, 100), wall.Points[0]);
        Assert.Equal(new MapPoint(600, 100), wall.Points[1]);
    }

    [Fact]
    public void MapFromJson_BasicWalls_MapsDimensionsAndWallTypes()
    {
        Assert.True(File.Exists(WallsFixturePath), $"Missing fixture: {WallsFixturePath}");
        var map = FoundrySceneMapper.MapFromJson(File.ReadAllText(WallsFixturePath));

        Assert.Equal("all-foundry-walls", map.Name);
        Assert.Equal(4000, map.Scene.Width);
        Assert.Equal(3000, map.Scene.Height);
        Assert.Equal(4000, map.Preview!.Width);
        Assert.Equal(3000, map.Preview.Height);
        Assert.Equal(1, map.Grid.CellSize);
        Assert.Equal(100, map.Grid.PixelsPerCell);
        Assert.Equal(40, map.Grid.Columns);
        Assert.Equal(30, map.Grid.Rows);
        Assert.Equal(7, map.Walls.Count);

        Assert.Equal(
            new HashSet<WallLineType>
            {
                WallLineType.Solid,
                WallLineType.Terrain,
                WallLineType.Invisible,
                WallLineType.Ethereal,
                WallLineType.Door,
                WallLineType.SecretDoor,
                WallLineType.Window,
            },
            map.Walls.Select(wall => wall.LineType).ToHashSet());

        // Fixture padding 0.25 → sceneX=1000, sceneY=800 (grid size 100).
        var solid = Assert.Single(map.Walls, wall => wall.LineType == WallLineType.Solid);
        Assert.Equal(2, solid.Points.Count);
        Assert.Equal(625, solid.Points[0].X);
        Assert.Equal(275, solid.Points[0].Y);
        Assert.Equal(650, solid.Points[1].X);
        Assert.Equal(813, solid.Points[1].Y);
    }

    [Fact]
    public void MapFromJson_BasicRegions_ExpandsShapesAndInfersTypes()
    {
        Assert.True(File.Exists(RegionsFixturePath), $"Missing fixture: {RegionsFixturePath}");
        var map = FoundrySceneMapper.MapFromJson(File.ReadAllText(RegionsFixturePath));

        Assert.Equal("Region Examples", map.Name);
        Assert.Equal(4000, map.Scene.Width);
        Assert.Equal(3000, map.Scene.Height);

        // 5 rectangles from first region + 2 rectangles + 1 polygon from second = 8
        Assert.Equal(8, map.Regions.Count);
        Assert.All(
            map.Regions.Take(5),
            region => Assert.Equal(RegionType.DifficultTerrain, region.RegionType));
        Assert.All(
            map.Regions.Skip(5),
            region => Assert.Equal(RegionType.GreaterDifficultTerrain, region.RegionType));

        // Fixture padding 0.25 → sceneX=1000, sceneY=800.
        var firstRect = map.Regions[0];
        Assert.Equal(4, firstRect.Points.Count);
        Assert.Equal(new MapPoint(200, 150), firstRect.Points[0]);
        Assert.Equal(new MapPoint(450, 150), firstRect.Points[1]);
        Assert.Equal(new MapPoint(450, 400), firstRect.Points[2]);
        Assert.Equal(new MapPoint(200, 400), firstRect.Points[3]);

        var polygon = map.Regions.Single(region => region.Points.Count > 4);
        Assert.True(polygon.Points.Count >= 3);
        Assert.Equal(1887.5, polygon.Points[0].X);
        Assert.Equal(1412.5, polygon.Points[0].Y);
    }

    [Fact]
    public void MapFromJson_CircleShape_BecomesPolygonRegion()
    {
        const string json = """
            {
              "name": "Floor 1",
              "width": 4800,
              "height": 6400,
              "padding": 0.25,
              "shiftX": 0,
              "shiftY": 0,
              "grid": { "size": 100 },
              "walls": [],
              "regions": [
                {
                  "name": "Circle Template [Demi]",
                  "color": "#400080",
                  "levels": [],
                  "shapes": [
                    { "type": "circle", "x": 5100, "y": 2900, "radius": 1200, "hole": false }
                  ]
                }
              ]
            }
            """;

        var map = FoundrySceneMapper.MapFromJson(json);

        // padding 0.25 / grid 100 → sceneX=1200, sceneY=1600; center becomes (3900, 1300)
        var region = Assert.Single(map.Regions);
        Assert.Equal("Circle Template [Demi]", region.Name);
        Assert.Equal(RegionType.Other, region.RegionType);
        Assert.Equal(32, region.Points.Count);
        Assert.Equal(5100, region.Points[0].X, precision: 6); // 3900 + 1200*cos(0)
        Assert.Equal(1300, region.Points[0].Y, precision: 6); // 1300 + 1200*sin(0)
    }

    [Fact]
    public void MapFromJson_FiltersWallsNotOnDefaultLevel()
    {
        const string json = """
            {
              "name": "Multi",
              "width": 1000,
              "height": 1000,
              "padding": 0,
              "shiftX": 0,
              "shiftY": 0,
              "initialLevel": "defaultLevel0000",
              "grid": { "size": 100 },
              "levels": ["defaultLevel0000", "otherLevel"],
              "walls": [
                { "c": [10, 20, 30, 40], "sight": 20, "light": 20, "sound": 20, "move": 20, "door": 0,
                  "levels": ["defaultLevel0000"] },
                { "c": [50, 60, 70, 80], "sight": 20, "light": 20, "sound": 20, "move": 20, "door": 0,
                  "levels": ["otherLevel"] },
                { "c": [1, 2, 3, 4], "sight": 20, "light": 20, "sound": 20, "move": 20, "door": 0,
                  "levels": [] }
              ],
              "regions": []
            }
            """;

        var map = FoundrySceneMapper.MapFromJson(json);

        Assert.Equal(2, map.Walls.Count);
        Assert.Equal(new MapPoint(10, 20), map.Walls[0].Points[0]);
        Assert.Equal(new MapPoint(1, 2), map.Walls[1].Points[0]);
    }

    [Fact]
    public void ResolveBackgroundPath_UrlDecodesAndStaysUnderDataRoot()
    {
        using var temp = new TempDir();
        var data = Path.Combine(temp.Path, "Data");
        var maps = Path.Combine(data, "Maps");
        Directory.CreateDirectory(maps);
        var image = Path.Combine(maps, "Abandoned Hut.jpg");
        File.WriteAllBytes(image, [0xFF, 0xD8]);

        var resolved = FoundrySceneMapper.ResolveBackgroundPath(data, "Maps/Abandoned%20Hut.jpg");

        Assert.Equal(Path.GetFullPath(image), resolved);
        Assert.Null(FoundrySceneMapper.ResolveBackgroundPath(data, "../outside.png"));
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "brickwork-scene-mapper-" + Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(Path);

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
                // best-effort
            }
        }
    }
}
