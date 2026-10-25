namespace Brickwork.Core.Models;

/// <summary>
/// Foundry scene levels (1-based) that walls, regions and lights appear on. An entity's level set
/// is empty when it is on every level, like Foundry's own <c>levels</c> field.
/// </summary>
public static class SceneLevels
{
    public const int MaxCount = 10;

    public static bool IsOn(IReadOnlySet<int> levels, int level) => levels.Count == 0 || levels.Contains(level);

    /// <summary>Like <see cref="IsOn(IReadOnlySet{int}, int)"/>, as exported with <paramref name="levelCount"/> levels.</summary>
    public static bool IsOn(IReadOnlySet<int> levels, int level, int levelCount)
    {
        var effective = Effective(levels, levelCount);
        return effective.Count == 0 || effective.Contains(level);
    }

    /// <summary>
    /// Puts an entity on <paramref name="level"/> or takes it off. Being on every level is stored as
    /// an empty set. Returns false (and changes nothing) when it would leave the entity on no level.
    /// </summary>
    public static bool Set(HashSet<int> levels, int level, bool on, int levelCount)
    {
        if (on)
        {
            if (levels.Count == 0)
            {
                return true;
            }

            levels.Add(level);
            if (Enumerable.Range(1, levelCount).All(levels.Contains))
            {
                levels.Clear();
            }

            return true;
        }

        var remaining = levels.Count == 0 ? Enumerable.Range(1, levelCount).ToHashSet() : levels.ToHashSet();
        remaining.Remove(level);
        if (!remaining.Any(candidate => candidate >= 1 && candidate <= levelCount))
        {
            return false;
        }

        levels.Clear();
        levels.UnionWith(remaining);
        return true;
    }

    /// <summary>
    /// The levels an entity is on, within <paramref name="levelCount"/>; empty means every level
    /// (also when it was only on levels that no longer exist).
    /// </summary>
    public static IReadOnlyList<int> Effective(IReadOnlySet<int> levels, int levelCount)
    {
        var within = levels.Where(level => level >= 1 && level <= levelCount).Order().ToList();
        return within.Count == levelCount ? [] : within;
    }
}
