using Brickwork.Core.Models;
using Brickwork.Inkarnate.Parsing;

namespace Brickwork.Inkarnate;

/// <summary>Adds an X of two solid walls at the centre of every tree stamp (see <see cref="TreeTrunks"/>).</summary>
internal static class TreeImportDetection
{
    public static int AddTrunkWalls(MapDocument map, IEnumerable<InkStamp> stamps, InkarnateImportOptions options)
    {
        if (!options.DetectTreesFromStamps || options.TreeTrunkSize <= 0 || map.Grid.CellSize <= 0)
        {
            return 0;
        }

        var include = NameKeywords.Parse(options.TreeKeywords);
        var exclude = NameKeywords.Parse(options.TreeExcludeKeywords);
        var hiddenLayers = map.Layers
            .Where(layer => !layer.IsVisible)
            .Select(layer => layer.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var trees = stamps.Where(stamp => NameKeywords.MatchesEither(stamp.Name, stamp.DefaultName, include, exclude)).ToList();

        // One group per tree kind and layer, so a single checkbox switches e.g. all palm trunks.
        var kinds = trees
            .GroupBy(stamp => (Layer: stamp.LayerId ?? string.Empty, Kind: KindName(stamp)))
            .OrderBy(group => group.Key.Layer, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Kind, StringComparer.Ordinal)
            .ToList();
        var added = 0;

        for (var kindIndex = 0; kindIndex < kinds.Count; kindIndex++)
        {
            var kind = kinds[kindIndex];
            var typicalScale = Median(kind.Select(stamp => stamp.Scale));
            var group = new EntityGroup
            {
                GroupId = TreeTrunks.GroupId(kindIndex),
                Name = $"{kind.Key.Kind} trunks",
                LayerId = kind.First().LayerId,
            };

            foreach (var stamp in kind.OrderBy(stamp => stamp.EntityId))
            {
                var isActive = stamp.IsEntityVisible &&
                               (stamp.LayerId is null || !hiddenLayers.Contains(stamp.LayerId));
                var sizeRatio = stamp.Scale / typicalScale;
                var unit = map.Grid.CellSize * sizeRatio;
                var sizeUnit = map.Grid.CellSize * TreeTrunks.SizeFactor(sizeRatio, options.TreeTrunkGrowthPercent);
                options.TreeAdjustments.TryGetValue(
                    TreeTrunks.VersionKey(stamp.AssetId, kind.Key.Kind),
                    out var adjustment);
                for (var arm = 0; arm < 2; arm++)
                {
                    var trunk = new TreeTrunk(
                        stamp.EntityId,
                        stamp.AssetId,
                        arm,
                        stamp.Position,
                        stamp.Angle,
                        unit,
                        sizeUnit,
                        options.TreeTrunkSize)
                    {
                        Scale = adjustment?.Scale ?? 1,
                        OffsetX = adjustment?.OffsetX ?? 0,
                        OffsetY = adjustment?.OffsetY ?? 0,
                    };
                    var (start, end) = TreeTrunks.BuildArm(trunk);
                    var wall = new Wall
                    {
                        EntityId = TreeTrunks.WallId(stamp.EntityId, arm),
                        Name = kind.Key.Kind,
                        LayerId = stamp.LayerId,
                        GroupId = group.GroupId,
                        Trunk = trunk,
                        LineType = WallLineType.Solid,
                        WallEnabled = true,
                        IsActive = isActive,
                        IsEntityVisible = stamp.IsEntityVisible,
                        Scale = 1,
                        Points = [start, end],
                    };
                    map.Walls.Add(wall);
                    group.MemberIds.Add(wall.EntityId);
                    added++;
                }
            }

            map.Groups.Add(group);
        }

        return added;
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToList();
        var middle = sorted.Count / 2;
        var median = sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2d;
        return median > 0 ? median : 1;
    }

    private static string KindName(InkStamp stamp) =>
        (string.IsNullOrWhiteSpace(stamp.Name) ? stamp.DefaultName : stamp.Name)?.Trim() ?? "Tree";
}
