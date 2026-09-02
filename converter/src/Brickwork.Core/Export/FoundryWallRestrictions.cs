using Brickwork.Core.Models;

namespace Brickwork.Core.Export;

public sealed record FoundryWallRestrictions(
    int Sight,
    int Light,
    int Sound,
    int Move,
    int Door,
    int? ThresholdLight = null,
    int? ThresholdSight = null,
    int? ThresholdSound = null,
    bool ThresholdAttenuation = false)
{
    public static FoundryWallRestrictions ForLineType(WallLineType lineType) =>
        lineType switch
        {
            WallLineType.Terrain => new(10, 10, 10, 20, 0),
            WallLineType.Invisible => new(0, 0, 0, 20, 0),
            WallLineType.Ethereal => new(20, 20, 0, 0, 0),
            WallLineType.Door => new(20, 20, 20, 20, 1),
            WallLineType.SecretDoor => new(20, 20, 20, 20, 2),
            WallLineType.Window => new(30, 30, 20, 20, 0, 10, 10, null, true),
            _ => new(20, 20, 20, 20, 0),
        };
}
