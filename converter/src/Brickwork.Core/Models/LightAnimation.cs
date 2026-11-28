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

    public const string DefaultTorchWords =
        "torch, fire, campfire, bonfire, fireplace, hearth, brazier, pyre, flame, flames";

    public const string DefaultFlickerWords =
        "candle, candelabra, chandelier, sconce, lantern, lamp, oil";

    public const string DefaultPulseWords =
        "glowing, glow, crystal, crystals, magic, rune, runes, arcane, lava, magma";

    public static IReadOnlyList<LightAnimation> All { get; } = Enum.GetValues<LightAnimation>();

    /// <summary>Guesses an animation from a light's name using the built-in word lists.</summary>
    public static LightAnimation Detect(string? name) =>
        Detect(
            name,
            LightKeywords.Parse(DefaultTorchWords),
            LightKeywords.Parse(DefaultFlickerWords),
            LightKeywords.Parse(DefaultPulseWords));

    /// <summary>Guesses an animation from a light's name; open flames win over enclosed ones.</summary>
    public static LightAnimation Detect(
        string? name,
        IReadOnlyList<string> torchWords,
        IReadOnlyList<string> flickerWords,
        IReadOnlyList<string> pulseWords)
    {
        if (LightKeywords.ContainsAnyWord(name, torchWords))
        {
            return LightAnimation.Torch;
        }

        if (LightKeywords.ContainsAnyWord(name, flickerWords))
        {
            return LightAnimation.Flicker;
        }

        return LightKeywords.ContainsAnyWord(name, pulseWords) ? LightAnimation.Pulse : LightAnimation.None;
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
