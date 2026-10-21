namespace Brickwork.Core.Models;

/// <summary>Default words that mark an object name as a light source; see <see cref="NameKeywords"/>.</summary>
public static class LightKeywords
{
    public const string DefaultInclude =
        "lantern, lamp, torch, candle, candelabra, chandelier, sconce, brazier, campfire, bonfire, fireplace, hearth, fire, lit, light, glowing";

    public const string DefaultExclude = "unlit, broken, extinguished, snuffed, burnt out";
}
