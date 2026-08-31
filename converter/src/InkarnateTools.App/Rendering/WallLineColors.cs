using InkarnateTools.Core.Models;
using SkiaSharp;

namespace InkarnateTools.App.Rendering;

public static class WallLineColors
{
    private static readonly SKColor InactiveColor = new(0x88, 0x88, 0x88, 0x88);

    private static readonly SKColor DefaultActiveColor = new(0xFF, 0x44, 0x44, 0xCC);
    private static readonly SKColor DoorActiveColor = new(0x44, 0x88, 0xFF, 0xCC);
    private static readonly SKColor TerrainActiveColor = new(0xFF, 0xCC, 0x44, 0xCC);

    public static SKColor ForLine(WallLineType lineType, bool isActive) =>
        !isActive
            ? InactiveColor
            : lineType switch
            {
                WallLineType.Door => DoorActiveColor,
                WallLineType.Terrain => TerrainActiveColor,
                _ => DefaultActiveColor,
            };

    public static SKColor FillForLine(WallLineType lineType, bool isActive)
    {
        var stroke = ForLine(lineType, isActive);
        return new SKColor(stroke.Red, stroke.Green, stroke.Blue, (byte)(stroke.Alpha / 3));
    }
}
