using Brickwork.Core.Geometry;

namespace Brickwork.Core.Models;

/// <summary>
/// Tree trunks as small X-shaped walls: two crossing solid walls at the centre of a tree stamp,
/// so tokens cannot see or walk through the trunk while the canopy stays open.
/// </summary>
public static class TreeTrunks
{
    public const string DefaultInclude =
        "tree, palm, oak, pine, birch, willow, fir, spruce, cypress, maple, elm, ash, beech, cedar, redwood, baobab, banyan, mangrove, sequoia, trunk";

    public const string DefaultExclude = "bush, shrub, fallen, log, stump, sapling, leaf, leaves, branch, canopy, fern, root, roots";

    /// <summary>Default length of each of the two crossing walls, in grid squares.</summary>
    public const double DefaultSize = 0.5;

    /// <summary>
    /// Trunk walls get negative ids derived from their stamp, so they never collide with
    /// Inkarnate entity ids or ids Brickwork allocates (both positive) and stay stable when the
    /// map is imported again.
    /// </summary>
    public static int WallId(int stampId, int arm) => -(stampId * 2 + arm);

    /// <summary>Negative group id for the trunks of one tree kind on one layer.</summary>
    public static int GroupId(int kindIndex) => -(1_500_000_000 + kindIndex);

    /// <summary>Default share of a tree's growth that its trunk follows, in percent.</summary>
    public const double DefaultGrowthPercent = 50;

    /// <summary>
    /// Trunk size factor for a tree <paramref name="sizeRatio"/> times its kind's typical size: the
    /// trunk grows by <paramref name="growthPercent"/> of the tree's growth, so at 50 % a tree twice
    /// as large gets a 1.5x trunk and a half-size tree a 0.75x trunk; 0 % keeps every trunk the same.
    /// </summary>
    public static double SizeFactor(double sizeRatio, double growthPercent) =>
        Math.Max(0.1, 1 + (sizeRatio - 1) * growthPercent / 100d);

    public static bool IsTrunk(Wall wall) => wall.Trunk is not null;

    /// <summary>
    /// Key of a tree version in the tree-trunk library: the Inkarnate asset id, which is the same in
    /// every map, or the tree's name when the stamp has no asset id.
    /// </summary>
    public static string VersionKey(int? assetId, string? name) =>
        assetId is int id
            ? $"asset:{id}"
            : $"name:{(name ?? string.Empty).Trim().ToLowerInvariant()}";

    public static string VersionKey(Wall wall) => VersionKey(wall.Trunk?.AssetId, wall.Name);

    /// <summary>Start and end of the trunk wall described by <paramref name="trunk"/>.</summary>
    public static (MapPoint Start, MapPoint End) BuildArm(TreeTrunk trunk)
    {
        var shifted = new MapPoint(
            trunk.StampCenter.X + trunk.OffsetX * trunk.Unit,
            trunk.StampCenter.Y + trunk.OffsetY * trunk.Unit);
        var center = MapPointTransforms.RotateAround(shifted, trunk.StampCenter, trunk.Angle);
        return BuildX(center, trunk.BaseSize * trunk.Scale * trunk.SizeUnit, trunk.Angle)[trunk.Arm];
    }

    /// <summary>Applies a version adjustment to a trunk wall and rebuilds its points.</summary>
    public static void Adjust(Wall wall, double scale, double offsetX, double offsetY)
    {
        if (wall.Trunk is not { } trunk || scale <= 0)
        {
            return;
        }

        wall.Trunk = trunk with { Scale = scale, OffsetX = offsetX, OffsetY = offsetY };
        var (start, end) = BuildArm(wall.Trunk);
        wall.Points.Clear();
        wall.Points.Add(start);
        wall.Points.Add(end);
    }

    /// <summary>The two crossing walls (as point pairs) of an X centred on <paramref name="center"/>.</summary>
    public static IReadOnlyList<(MapPoint Start, MapPoint End)> BuildX(
        MapPoint center,
        double armLength,
        double angleDegrees)
    {
        var half = armLength / 2d;
        var result = new List<(MapPoint, MapPoint)>(2);
        foreach (var direction in new[] { 45d, 135d })
        {
            var radians = (direction + angleDegrees) * Math.PI / 180d;
            var dx = Math.Cos(radians) * half;
            var dy = Math.Sin(radians) * half;
            result.Add((new MapPoint(center.X - dx, center.Y - dy), new MapPoint(center.X + dx, center.Y + dy)));
        }

        return result;
    }
}
