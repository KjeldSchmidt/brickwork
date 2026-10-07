using Brickwork.Core.Models;
using SkiaSharp;

namespace Brickwork.App.Rendering;

public static class RegionColors
{
    private static readonly SKColor InactiveStroke = new(0x88, 0x88, 0x88, 0x88);
    private static readonly SKColor InactiveFill = new(0x88, 0x88, 0x88, 0x33);

    public static SKColor ForStroke(RegionType regionType, bool isActive) =>
        !isActive
            ? InactiveStroke
            : regionType switch
            {
                RegionType.DifficultTerrain => new SKColor(0x81, 0xB9, 0x0C, 0xCC),
                _ => new SKColor(0xAA, 0xAA, 0xBB, 0xCC),
            };

    public static SKColor ForFill(RegionType regionType, bool isActive) =>
        !isActive
            ? InactiveFill
            : regionType switch
            {
                RegionType.DifficultTerrain => new SKColor(0x81, 0xB9, 0x0C, 0x44),
                _ => new SKColor(0xAA, 0xAA, 0xBB, 0x44),
            };

    public static SKColor ForHighlight() => new SKColor(0xAA, 0xDD, 0xFF, 0xCC);
}
