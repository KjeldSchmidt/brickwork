namespace Brickwork.Core.Models;

public static class MapExportFilters
{
    public static IEnumerable<Wall> ExportableWalls(this MapDocument map) =>
        map.Walls.Where(wall => wall.IsActive && wall.WallEnabled);

    public static IEnumerable<WallPortal> ActivePortals(this Wall wall) =>
        wall.Portals.Where(portal => portal.IsActive);

    public static bool HasPortals(this Wall wall) =>
        wall.Portals.Any(portal => portal.Width > 0);

    public static bool HasActivePortals(this Wall wall) =>
        wall.Portals.Any(portal => portal.IsActive && portal.Width > 0);
}
