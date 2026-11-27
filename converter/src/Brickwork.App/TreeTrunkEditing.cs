using Brickwork.Core.Models;

namespace Brickwork.App;

/// <summary>
/// Changes the scale/offset of whole tree versions: in the open map and in the tree-trunk library
/// (tree-trunks.json), so other maps using the same tree image get the change too. Callers wrap
/// map changes in <see cref="EditorSession.Execute"/> or a gesture for undo.
/// </summary>
public static class TreeTrunkEditing
{
    public static IReadOnlyList<Wall> TrunksOfVersion(MapDocument map, string versionKey) =>
        map.Walls.Where(wall => TreeTrunks.IsTrunk(wall) && TreeTrunks.VersionKey(wall) == versionKey).ToList();

    public static IReadOnlyList<string> VersionKeys(IEnumerable<Wall> walls) =>
        walls.Where(TreeTrunks.IsTrunk).Select(TreeTrunks.VersionKey).Distinct().ToList();

    /// <summary>
    /// Sets new values for the given versions: <paramref name="change"/> maps a trunk's current
    /// (scale, offsetX, offsetY) to the new ones. Updates the library and rebuilds every trunk wall
    /// of those versions in <paramref name="map"/>.
    /// </summary>
    public static void AdjustVersions(
        EditorSession session,
        MapDocument map,
        IEnumerable<string> versionKeys,
        Func<TreeTrunk, (double Scale, double OffsetX, double OffsetY)> change)
    {
        foreach (var key in versionKeys.Distinct())
        {
            var walls = TrunksOfVersion(map, key);
            if (walls.Count == 0 || walls[0].Trunk is not { } sample)
            {
                continue;
            }

            var (scale, offsetX, offsetY) = change(sample);
            session.SetTreeAdjustment(key, new TreeTrunkAdjustment
            {
                Name = walls[0].DisplayName,
                Scale = scale,
                OffsetX = offsetX,
                OffsetY = offsetY,
            });

            foreach (var wall in walls)
            {
                // Rounded like the library file, so the map matches what a re-import produces.
                var (wallScale, wallOffsetX, wallOffsetY) = change(wall.Trunk!);
                TreeTrunks.Adjust(
                    wall,
                    Math.Round(wallScale, 3),
                    Math.Round(wallOffsetX, 3),
                    Math.Round(wallOffsetY, 3));
            }
        }
    }

    /// <summary>Returns the given versions to their built-in (or unadjusted) values, in the library and the map.</summary>
    public static void ResetVersions(EditorSession session, MapDocument map, IEnumerable<string> versionKeys)
    {
        foreach (var key in versionKeys.Distinct())
        {
            var walls = TrunksOfVersion(map, key);
            var value = session.ResetTreeAdjustment(key, walls.FirstOrDefault()?.DisplayName);
            foreach (var wall in walls)
            {
                TreeTrunks.Adjust(wall, value.Scale, value.OffsetX, value.OffsetY);
            }
        }
    }
}
