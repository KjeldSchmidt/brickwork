using System.Globalization;
using System.Text;
using Brickwork.Core.Models;

namespace Brickwork.App;

internal static class RegionDebugFormatter
{
    public static string Format(Region region)
    {
        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Entity ID: {region.EntityId}");
        builder.AppendLine($"Name: {region.DisplayName}");

        if (!string.IsNullOrWhiteSpace(region.LayerId))
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Layer ID: {region.LayerId}");
        }

        if (region.GroupId is int groupId)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Group ID: {groupId}");
        }

        builder.AppendLine(CultureInfo.InvariantCulture, $"Type: {region.RegionType}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Active: {FormatBool(region.IsActive)}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Entity visible: {FormatBool(region.IsEntityVisible)}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Vertices: {region.Points.Count}");

        if (region.Points.Count > 0)
        {
            builder.AppendLine("Points (scene):");
            for (var i = 0; i < region.Points.Count; i++)
            {
                var point = region.Points[i];
                builder.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"  [{i}] ({point.X:0.##}, {point.Y:0.##})");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatBool(bool value) => value ? "yes" : "no";
}
