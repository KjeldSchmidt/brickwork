using System.Text.Json;
using Brickwork.Core.Export;
using Brickwork.Core.Geometry;
using Brickwork.Core.Models;
using Xunit;

namespace Brickwork.Core.Tests;

public class FoundryWallRestrictionsTests
{
    private static string ReferenceScenePath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "fvtt-scene-basic-walls.json"));

    public static IEnumerable<object[]> ReferenceWallTypes =>
    [
        [WallLineType.Solid, 0],
        [WallLineType.Terrain, 1],
        [WallLineType.Invisible, 2],
        [WallLineType.Ethereal, 3],
        [WallLineType.Door, 4],
        [WallLineType.SecretDoor, 5],
        [WallLineType.Window, 6],
    ];

    [Theory]
    [MemberData(nameof(ReferenceWallTypes))]
    public void ForLineType_MatchesFoundryReferenceScene(WallLineType lineType, int referenceWallIndex)
    {
        var referenceWall = LoadReferenceWall(referenceWallIndex);
        var restrictions = FoundryWallRestrictions.ForLineType(lineType);

        Assert.Equal(referenceWall.Sight, restrictions.Sight);
        Assert.Equal(referenceWall.Light, restrictions.Light);
        Assert.Equal(referenceWall.Sound, restrictions.Sound);
        Assert.Equal(referenceWall.Move, restrictions.Move);
        Assert.Equal(referenceWall.Door, restrictions.Door);
        Assert.Equal(referenceWall.ThresholdLight, restrictions.ThresholdLight);
        Assert.Equal(referenceWall.ThresholdSight, restrictions.ThresholdSight);
        Assert.Equal(referenceWall.ThresholdSound, restrictions.ThresholdSound);
        Assert.Equal(referenceWall.ThresholdAttenuation, restrictions.ThresholdAttenuation);
    }

    [Theory]
    [MemberData(nameof(ReferenceWallTypes))]
    public void BuildFromWall_ExportsMatchingRestrictions(WallLineType lineType, int referenceWallIndex)
    {
        var referenceWall = LoadReferenceWall(referenceWallIndex);
        var map = CreateSingleWallMap(lineType);
        var transform = SceneTransform.FromMap(map)!;

        var segment = Assert.Single(FoundryWallSegmentBuilder.BuildFromWall(map.Walls[0], transform));

        Assert.Equal(referenceWall.Sight, segment.Sight);
        Assert.Equal(referenceWall.Light, segment.Light);
        Assert.Equal(referenceWall.Sound, segment.Sound);
        Assert.Equal(referenceWall.Move, segment.Move);
        Assert.Equal(referenceWall.Door, segment.Door);
        Assert.Equal(referenceWall.ThresholdLight, segment.ThresholdLight);
        Assert.Equal(referenceWall.ThresholdSight, segment.ThresholdSight);
        Assert.Equal(referenceWall.ThresholdSound, segment.ThresholdSound);
        Assert.Equal(referenceWall.ThresholdAttenuation, segment.ThresholdAttenuation);
    }

    private static ReferenceWall LoadReferenceWall(int index)
    {
        Assert.True(File.Exists(ReferenceScenePath), $"Missing test resource: {ReferenceScenePath}");

        using var document = JsonDocument.Parse(File.ReadAllText(ReferenceScenePath));
        var wall = document.RootElement.GetProperty("walls")[index];
        var threshold = wall.GetProperty("threshold");

        return new ReferenceWall(
            wall.GetProperty("sight").GetInt32(),
            wall.GetProperty("light").GetInt32(),
            wall.GetProperty("sound").GetInt32(),
            wall.GetProperty("move").GetInt32(),
            wall.GetProperty("door").GetInt32(),
            GetNullableInt(threshold, "light"),
            GetNullableInt(threshold, "sight"),
            GetNullableInt(threshold, "sound"),
            threshold.GetProperty("attenuation").GetBoolean());
    }

    private static MapDocument CreateSingleWallMap(WallLineType lineType) =>
        new()
        {
            Scene = new SceneDimensions { Width = 1000, Height = 1000 },
            Preview = new PreviewDimensions { Width = 1000, Height = 1000 },
            Walls =
            [
                new Wall
                {
                    EntityId = 1,
                    LineType = lineType,
                    Origin = new MapPoint(0, 0),
                    Scale = 1,
                    Points =
                    [
                        new MapPoint(0, 0),
                        new MapPoint(1000, 0),
                    ],
                },
            ],
        };

    private static int? GetNullableInt(JsonElement parent, string propertyName)
    {
        var value = parent.GetProperty(propertyName);
        return value.ValueKind == JsonValueKind.Null ? null : value.GetInt32();
    }

    private sealed record ReferenceWall(
        int Sight,
        int Light,
        int Sound,
        int Move,
        int Door,
        int? ThresholdLight,
        int? ThresholdSight,
        int? ThresholdSound,
        bool ThresholdAttenuation);
}
