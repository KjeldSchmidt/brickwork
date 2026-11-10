using Brickwork.Core.Models;
using Brickwork.Inkarnate.Parsing;

namespace Brickwork.Inkarnate;

/// <summary>Creates lights from stamps whose names look like light sources (lanterns, torches, …).</summary>
internal static class LightImportDetection
{
    public static int AddLightsFromStamps(
        MapDocument map,
        IEnumerable<InkStamp> stamps,
        InkarnateImportOptions options)
    {
        if (!options.DetectLightsFromStamps)
        {
            return 0;
        }

        var include = LightKeywords.Parse(options.LightKeywords);
        var exclude = LightKeywords.Parse(options.LightExcludeKeywords);
        var hiddenLayers = map.Layers
            .Where(layer => !layer.IsVisible)
            .Select(layer => layer.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingIds = map.Lights.Select(light => light.EntityId).ToHashSet();
        var added = 0;

        foreach (var stamp in stamps)
        {
            var name = string.IsNullOrWhiteSpace(stamp.Name) ? stamp.DefaultName?.Trim() : stamp.Name.Trim();
            if (existingIds.Contains(stamp.EntityId) || !IsLightStamp(stamp, include, exclude))
            {
                continue;
            }

            var sourceName = stamp.DefaultName?.Trim();
            map.Lights.Add(new LightSource
            {
                EntityId = stamp.EntityId,
                Name = name,
                SourceName = sourceName,
                LayerId = stamp.LayerId,
                GroupId = stamp.GroupId,
                Position = stamp.Position,
                IsActive = stamp.IsEntityVisible &&
                           (stamp.LayerId is null || !hiddenLayers.Contains(stamp.LayerId)),
                BrightRadius = options.LightBrightRadius,
                DimRadius = options.LightDimRadius,
                Animation = options.DetectLightAnimations
                    ? DetectAnimation(name, sourceName)
                    : LightAnimation.None,
            });
            added++;
        }

        return added;
    }

    /// <summary>
    /// Either the custom or the asset name may mark a light, but an exclude word in either one
    /// (e.g. a lantern renamed to "Lantern (broken)") wins.
    /// </summary>
    private static bool IsLightStamp(InkStamp stamp, IReadOnlyList<string> include, IReadOnlyList<string> exclude) =>
        (LightKeywords.IsLight(stamp.Name, include, exclude) ||
         LightKeywords.IsLight(stamp.DefaultName, include, exclude)) &&
        !LightKeywords.IsLight(stamp.Name, exclude, []) &&
        !LightKeywords.IsLight(stamp.DefaultName, exclude, []);

    private static LightAnimation DetectAnimation(string? name, string? sourceName)
    {
        var fromName = LightAnimations.Detect(name);
        return fromName != LightAnimation.None ? fromName : LightAnimations.Detect(sourceName);
    }
}
