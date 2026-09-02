using InkarnateTools.Core.Models;
using SkiaSharp;

namespace InkarnateTools.App.Rendering;

public static class WallLineColors
{
    private static readonly SKColor InactiveColor = new(0x88, 0x88, 0x88, 0x88);

    public static SKColor ForLine(WallLineType lineType, bool isActive) =>
        !isActive
            ? InactiveColor
            : lineType switch
            {
                WallLineType.Solid => new SKColor(0xFF, 0xFF, 0xBB, 0xCC),
                WallLineType.Terrain => new SKColor(0x81, 0xB9, 0x0C, 0xCC),
                WallLineType.Invisible => new SKColor(0x77, 0xE7, 0xE8, 0xCC),
                WallLineType.Ethereal => new SKColor(0xCA, 0x81, 0xFF, 0xCC),
                WallLineType.Door => new SKColor(0x66, 0x66, 0xEE, 0xCC),
                WallLineType.SecretDoor => new SKColor(0xA6, 0x12, 0xD4, 0xCC),
                WallLineType.Window => new SKColor(0xC7, 0xD8, 0xFF, 0xCC),
                _ => new SKColor(0xFF, 0xFF, 0xBB, 0xCC),
            };

    public static SKColor ForHighlight() => new SKColor(0xAA, 0xDD, 0xFF, 0xCC);

    public static SKColor FillForLine(WallLineType lineType, bool isActive)
    {
        var stroke = ForLine(lineType, isActive);
        return new SKColor(stroke.Red, stroke.Green, stroke.Blue, (byte)(stroke.Alpha / 3));
    }
}
