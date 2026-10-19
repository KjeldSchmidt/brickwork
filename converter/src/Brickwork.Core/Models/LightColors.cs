using System.Globalization;

namespace Brickwork.Core.Models;

public static class LightColors
{
    /// <summary>Returns <paramref name="color"/> as lowercase <c>#rrggbb</c>, or the default light color if invalid.</summary>
    public static string Normalize(string? color) =>
        TryParse(color, out var red, out var green, out var blue)
            ? $"#{red:x2}{green:x2}{blue:x2}"
            : LightSource.DefaultColor;

    public static bool TryParse(string? color, out byte red, out byte green, out byte blue)
    {
        red = green = blue = 0;
        var hex = color?.Trim().TrimStart('#');
        if (hex is not { Length: 6 } ||
            !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        red = (byte)(value >> 16);
        green = (byte)(value >> 8);
        blue = (byte)value;
        return true;
    }
}
