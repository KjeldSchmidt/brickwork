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
    private static readonly WallLineType[] InferOrder =
    [
        WallLineType.Window,
        WallLineType.Terrain,
        WallLineType.Invisible,
        WallLineType.Ethereal,
        WallLineType.Door,
        WallLineType.SecretDoor,
        WallLineType.Solid,
    ];

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

    /// <summary>
    /// Reverse of <see cref="ForLineType"/>, aligned with Foundry's
    /// <c>Wall#getWallCategory</c>: doors first, then sense/move categories, then exact
    /// preset match; unknown combinations become <see cref="WallLineType.Solid"/>.
    /// </summary>
    public static WallLineType InferLineType(FoundryWallRestrictions restrictions)
    {
        if (restrictions.Door == 1)
        {
            return WallLineType.Door;
        }

        if (restrictions.Door == 2)
        {
            return WallLineType.SecretDoor;
        }

        // Foundry EDGE_SENSE_TYPES: NONE=0, LIMITED=10, NORMAL=20, PROXIMITY=30, DISTANCE=40.
        // Category rules mirror client Wall#getWallCategory so hand-tuned walls still classify.
        if (restrictions.Sight == 0)
        {
            if (restrictions.Light == 0 &&
                restrictions.Sound == 0 &&
                restrictions.Move == 0)
            {
                // Foundry "blank" — no useful restriction; treat as solid for editing.
                return WallLineType.Solid;
            }

            return WallLineType.Invisible;
        }

        if (restrictions.Sight == 10)
        {
            return WallLineType.Terrain;
        }

        if (restrictions.Sight is 30 or 40)
        {
            return WallLineType.Window;
        }

        // Foundry: any wall with move === NONE is ethereal (regardless of sound/thresholds).
        if (restrictions.Move == 0)
        {
            return WallLineType.Ethereal;
        }

        foreach (var lineType in InferOrder)
        {
            if (Matches(ForLineType(lineType), restrictions))
            {
                return lineType;
            }
        }

        return WallLineType.Solid;
    }

    private static bool Matches(FoundryWallRestrictions expected, FoundryWallRestrictions actual) =>
        expected.Sight == actual.Sight &&
        expected.Light == actual.Light &&
        expected.Sound == actual.Sound &&
        expected.Move == actual.Move &&
        expected.Door == actual.Door &&
        expected.ThresholdLight == actual.ThresholdLight &&
        expected.ThresholdSight == actual.ThresholdSight &&
        expected.ThresholdSound == actual.ThresholdSound &&
        expected.ThresholdAttenuation == actual.ThresholdAttenuation;
}
