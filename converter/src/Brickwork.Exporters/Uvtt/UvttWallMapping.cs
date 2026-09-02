using Brickwork.Core.Models;

namespace Brickwork.Exporters.Uvtt;

internal enum UvttWallCategory
{
    LineOfSight,
    ObjectsLineOfSight,
    Portal,
}

internal static class UvttWallMapping
{
    public static UvttWallCategory ExportCategory(WallLineType lineType) =>
        lineType switch
        {
            WallLineType.Door or WallLineType.SecretDoor => UvttWallCategory.Portal,
            WallLineType.Window or WallLineType.Ethereal => UvttWallCategory.ObjectsLineOfSight,
            _ => UvttWallCategory.LineOfSight,
        };

    public static WallLineType ImportLineType(UvttWallCategory category) =>
        category switch
        {
            UvttWallCategory.ObjectsLineOfSight => WallLineType.Window,
            UvttWallCategory.Portal => WallLineType.Door,
            _ => WallLineType.Solid,
        };
}
