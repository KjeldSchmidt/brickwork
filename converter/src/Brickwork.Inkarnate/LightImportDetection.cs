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

        var include = NameKeywords.Parse(options.LightKeywords);
        var exclude = NameKeywords.Parse(options.LightExcludeKeywords);
        var hiddenLayers = map.Layers
            .Where(layer => !layer.IsVisible)
            .Select(layer => layer.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingIds = map.Lights.Select(light => light.EntityId).ToHashSet();
        var added = 0;

        foreach (var stamp in stamps)
        {
            var name = string.IsNullOrWhiteSpace(stamp.Name) ? stamp.DefaultName?.Trim() : stamp.Name.Trim();
            if (existingIds.Contains(stamp.EntityId) ||
                !NameKeywords.MatchesEither(stamp.Name, stamp.DefaultName, include, exclude))
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
                Color = LightColors.Normalize(options.LightColor),
                Animation = options.DetectLightAnimations
                    ? DetectAnimation(name, sourceName)
                    : LightAnimation.None,
            });
            added++;
        }

        return added;
    }

    private static LightAnimation DetectAnimation(string? name, string? sourceName)
    {
        var fromName = LightAnimations.Detect(name);
        return fromName != LightAnimation.None ? fromName : LightAnimations.Detect(sourceName);
    }
}
