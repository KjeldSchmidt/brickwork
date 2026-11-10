namespace Brickwork.Core.Models;

public enum LightAnimation
{
    None,

    /// <summary>Strong, irregular flicker of an open flame (Foundry "Torch").</summary>
    Torch,

    /// <summary>Gentle flicker of a small or enclosed flame (Foundry "Flickering Light").</summary>
    Flicker,

    /// <summary>Slow brightening and dimming, e.g. glowing crystals (Foundry "Pulse").</summary>
    Pulse,
}

public static class LightAnimations
{
    public const int DefaultSpeed = 3;

    public const int DefaultIntensity = 3;

    private static readonly string[] TorchWords =
        ["torch", "fire", "campfire", "bonfire", "fireplace", "hearth", "brazier", "pyre", "flame", "flames"];

    private static readonly string[] FlickerWords =
        ["candle", "candelabra", "chandelier", "sconce", "lantern", "lamp", "oil"];

    private static readonly string[] PulseWords =
        ["glowing", "glow", "crystal", "crystals", "magic", "rune", "runes", "arcane", "lava", "magma"];

    public static IReadOnlyList<LightAnimation> All { get; } = Enum.GetValues<LightAnimation>();

    /// <summary>Guesses an animation from a light's name; open flames win over enclosed ones.</summary>
    public static LightAnimation Detect(string? name)
    {
        if (LightKeywords.ContainsAnyWord(name, TorchWords))
        {
            return LightAnimation.Torch;
        }

        if (LightKeywords.ContainsAnyWord(name, FlickerWords))
        {
            return LightAnimation.Flicker;
        }

        return LightKeywords.ContainsAnyWord(name, PulseWords) ? LightAnimation.Pulse : LightAnimation.None;
    }

    /// <summary>Foundry <c>config.animation.type</c>, or null for no animation.</summary>
    public static string? ToFoundryType(LightAnimation animation) =>
        animation switch
        {
            LightAnimation.Torch => "torch",
            LightAnimation.Flicker => "flame",
            LightAnimation.Pulse => "pulse",
            _ => null,
        };

    public static string DisplayName(LightAnimation animation) =>
        animation switch
        {
            LightAnimation.Torch => "Torch",
            LightAnimation.Flicker => "Flicker",
            LightAnimation.Pulse => "Pulse",
            _ => "None",
        };
}
