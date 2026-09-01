using System.Globalization;
using System.Text;
using InkarnateTools.Core.Geometry;
using InkarnateTools.Core.Models;

namespace InkarnateTools.App;

internal static class WallDebugFormatter
{
    public static string Format(Wall wall, WallPortal? focusedPortal)
    {
        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Entity ID: {wall.EntityId}");
        builder.AppendLine($"Name: {wall.DisplayName}");

        if (!string.IsNullOrWhiteSpace(wall.LayerId))
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Layer ID: {wall.LayerId}");
        }

        if (wall.GroupId is int groupId)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Group ID: {groupId}");
        }

        builder.AppendLine(CultureInfo.InvariantCulture, $"Closed: {(wall.IsClosed ? "yes" : "no")}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Line type: {wall.LineType}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Wall enabled: {FormatBool(wall.WallEnabled)}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Active: {FormatBool(wall.IsActive)}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Vertices: {wall.Points.Count} (raw: {wall.RawPoints.Count})");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Portals: {wall.Portals.Count}");

        var totalLength = WallPolylineEdges.TotalLength(wall.Points, wall.IsClosed);
        builder.AppendLine(CultureInfo.InvariantCulture, $"Path length: {totalLength:0.##} scene units");

        var wallSegments = WallPathSegmentBuilder.BuildSegments(wall).Count;
        var portalSegments = WallPathSegmentBuilder.BuildPortalSegments(wall).Count;
        builder.AppendLine(CultureInfo.InvariantCulture, $"Export segments: {wallSegments} wall, {portalSegments} portal");

        if (!string.IsNullOrWhiteSpace(wall.PathData))
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Path data: {wall.PathData}");
        }

        builder.AppendLine(FormatTransform(wall));

        if (focusedPortal is not null)
        {
            builder.AppendLine();
            builder.AppendLine(FormatPortal(wall, focusedPortal));
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatPortal(Wall wall, WallPortal portal)
    {
        var builder = new StringBuilder();
        var portalLabel = string.IsNullOrWhiteSpace(portal.Id) ? "(no id)" : portal.Id;
        builder.AppendLine(CultureInfo.InvariantCulture, $"Focused gap: {portalLabel}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"  Line type: {portal.LineType}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"  Active: {FormatBool(portal.IsActive)}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"  Width: {portal.Width:0.##}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"  Anchor (local): ({portal.Anchor.X:0.##}, {portal.Anchor.Y:0.##})");

        if (WallPathSegmentBuilder.TryGetPortalArcInterval(wall, portal, out var start, out var end))
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"  Arc interval: [{start:0.##}, {end:0.##}]");
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatTransform(Wall wall)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Transform: angle={wall.Angle:0.##}° scale={wall.Scale:0.##} thickness={wall.WallThickness:0.##} (scene {wall.SceneThickness:0.##})");
    }

    private static string FormatBool(bool value) => value ? "yes" : "no";
}
