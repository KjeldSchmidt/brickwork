namespace InkarnateTools.Core.Models;

public static class MapExportFilters
{
    public static IEnumerable<Wall> ExportableWalls(this MapDocument map) =>
        map.Walls.Where(wall => wall.IsActive && wall.WallEnabled);
}
