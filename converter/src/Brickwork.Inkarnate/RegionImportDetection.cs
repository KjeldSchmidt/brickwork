using System.Text;
using Brickwork.Core.Models;

namespace Brickwork.Inkarnate;

public static class RegionImportDetection
{
    public static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    public static bool MatchesRegionName(string? value)
    {
        var normalized = NormalizeName(value);
        if (normalized.Length == 0)
        {
            return false;
        }

        return InferRegionTypeFromNormalizedName(normalized) is not null;
    }

    /// <summary>
    /// Infers a region type from a display name, or null when the name is not region-like.
    /// </summary>
    public static RegionType? InferRegionTypeFromName(string? value)
    {
        var normalized = NormalizeName(value);
        return normalized.Length == 0 ? null : InferRegionTypeFromNormalizedName(normalized);
    }

    public static int PromoteDetectedRegions(
        MapDocument map,
        IReadOnlyDictionary<int, string> pathWallTypes,
        InkarnateImportOptions options)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(pathWallTypes);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.AnyRegionDetectionEnabled)
        {
            return 0;
        }

        var layersById = map.Layers.ToDictionary(layer => layer.Id, StringComparer.OrdinalIgnoreCase);
        var groupsById = map.Groups.ToDictionary(group => group.GroupId);
        var promoted = 0;

        foreach (var wall in map.Walls.ToList())
        {
            if (!wall.IsClosed || wall.Points.Count < 3)
            {
                continue;
            }

            pathWallTypes.TryGetValue(wall.EntityId, out var wallType);
            var layerName = wall.LayerId is not null && layersById.TryGetValue(wall.LayerId, out var layer)
                ? layer.Name
                : null;
            var groupName = wall.GroupId is int groupId && groupsById.TryGetValue(groupId, out var group)
                ? group.Name
                : null;

            var matchedByColor = options.DetectRegionsFromColorPaths &&
                                 string.Equals(wallType, "color", StringComparison.OrdinalIgnoreCase);
            var matchedByContainer = options.DetectRegionsFromNamedContainers &&
                                     (MatchesRegionName(layerName) || MatchesRegionName(groupName));
            var matchedByPathName = options.DetectRegionsFromPathNames && MatchesRegionName(wall.Name);

            if (!matchedByColor && !matchedByContainer && !matchedByPathName)
            {
                continue;
            }

            var regionType = ResolveRegionType(
                matchedByPathName ? wall.Name : null,
                matchedByContainer ? layerName : null,
                matchedByContainer ? groupName : null);

            var region = new Region
            {
                EntityId = wall.EntityId,
                Name = wall.Name,
                LayerId = wall.LayerId,
                GroupId = wall.GroupId,
                IsActive = wall.IsActive,
                IsEntityVisible = wall.IsEntityVisible,
                RegionType = regionType,
                Points = wall.Points.ToList(),
            };

            if (!map.Walls.Remove(wall))
            {
                var match = map.Walls.FirstOrDefault(candidate => candidate.EntityId == wall.EntityId);
                if (match is null || !map.Walls.Remove(match))
                {
                    continue;
                }
            }

            map.Regions.Add(region);
            promoted++;
        }

        return promoted;
    }

    private static RegionType ResolveRegionType(string? pathName, string? layerName, string? groupName)
    {
        RegionType? best = null;
        foreach (var candidate in new[] { pathName, layerName, groupName })
        {
            var inferred = InferRegionTypeFromName(candidate);
            if (inferred is null)
            {
                continue;
            }

            if (inferred == RegionType.GreaterDifficultTerrain)
            {
                return RegionType.GreaterDifficultTerrain;
            }

            best ??= inferred;
        }

        return best ?? RegionType.DifficultTerrain;
    }

    private static RegionType? InferRegionTypeFromNormalizedName(string normalized)
    {
        if (normalized.Contains("greaterdifficult", StringComparison.Ordinal) ||
            (normalized.Contains("greater", StringComparison.Ordinal) &&
             normalized.Contains("difficult", StringComparison.Ordinal)))
        {
            return RegionType.GreaterDifficultTerrain;
        }

        if (normalized.Contains("difficultterrain", StringComparison.Ordinal) ||
            normalized.Contains("difficult", StringComparison.Ordinal))
        {
            return RegionType.DifficultTerrain;
        }

        if (normalized.Contains("regions", StringComparison.Ordinal) ||
            normalized.Contains("region", StringComparison.Ordinal))
        {
            return RegionType.DifficultTerrain;
        }

        return null;
    }
}
