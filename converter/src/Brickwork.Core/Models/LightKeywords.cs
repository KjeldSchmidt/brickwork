namespace Brickwork.Core.Models;

/// <summary>
/// Decides from an object name (e.g. an Inkarnate stamp asset name) whether it is a light source.
/// Keywords match whole words, case-insensitively, including a plural "s"/"es".
/// </summary>
public static class LightKeywords
{
    public const string DefaultInclude =
        "lantern, lamp, torch, candle, candelabra, chandelier, sconce, brazier, campfire, bonfire, fireplace, hearth, fire, lit, light, glowing";

    public const string DefaultExclude = "unlit, broken, extinguished, snuffed, burnt out";

    public static IReadOnlyList<string> Parse(string? keywords) =>
        string.IsNullOrWhiteSpace(keywords)
            ? []
            : keywords
                .Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(keyword => keyword.ToLowerInvariant())
                .Distinct()
                .ToList();

    public static bool IsLight(string? name, IReadOnlyList<string> include, IReadOnlyList<string> exclude)
    {
        var words = SplitWords(name);
        return words.Count > 0 && ContainsAny(words, include) && !ContainsAny(words, exclude);
    }

    /// <summary>True when <paramref name="name"/> contains one of <paramref name="keywords"/> as a whole word.</summary>
    public static bool ContainsAnyWord(string? name, IReadOnlyList<string> keywords)
    {
        var words = SplitWords(name);
        return words.Count > 0 && ContainsAny(words, keywords);
    }

    private static bool ContainsAny(IReadOnlyList<string> words, IReadOnlyList<string> keywords)
    {
        foreach (var keyword in keywords)
        {
            var keywordWords = SplitWords(keyword);
            if (keywordWords.Count == 0)
            {
                continue;
            }

            for (var start = 0; start + keywordWords.Count <= words.Count; start++)
            {
                var matched = true;
                for (var offset = 0; offset < keywordWords.Count && matched; offset++)
                {
                    matched = WordMatches(words[start + offset], keywordWords[offset]);
                }

                if (matched)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool WordMatches(string word, string keyword) =>
        word == keyword || word == keyword + "s" || word == keyword + "es";

    private static List<string> SplitWords(string? value)
    {
        var words = new List<string>();
        if (string.IsNullOrWhiteSpace(value))
        {
            return words;
        }

        var start = -1;
        for (var i = 0; i <= value.Length; i++)
        {
            var isLetter = i < value.Length && char.IsLetterOrDigit(value[i]);
            if (isLetter && start < 0)
            {
                start = i;
            }
            else if (!isLetter && start >= 0)
            {
                words.Add(value[start..i].ToLowerInvariant());
                start = -1;
            }
        }

        return words;
    }
}
