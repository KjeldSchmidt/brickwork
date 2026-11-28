using System.Globalization;
using System.Text;
using Brickwork.Core.Models;

namespace Brickwork.App;

internal static class LightDebugFormatter
{
    public static string Format(LightSource light)
    {
        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Entity ID: {light.EntityId}");
        builder.AppendLine($"Name: {light.DisplayName}");

        if (!string.IsNullOrWhiteSpace(light.SourceName))
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Source name: {light.SourceName}");
        }

        builder.AppendLine(CultureInfo.InvariantCulture, $"Kind: {light.KindName}");

        if (!string.IsNullOrWhiteSpace(light.LayerId))
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Layer ID: {light.LayerId}");
        }

        if (light.GroupId is int groupId)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Group ID: {groupId}");
        }

        builder.AppendLine(CultureInfo.InvariantCulture, $"Active: {FormatBool(light.IsActive)}");
        builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"Position (scene): ({light.Position.X:0.##}, {light.Position.Y:0.##})");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Bright radius: {light.BrightRadius:0.##} squares");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Dim radius: {light.DimRadius:0.##} squares");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Color: {LightColors.Normalize(light.Color)}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Animation: {LightAnimations.DisplayName(light.Animation)}");
        if (light.Animation != LightAnimation.None)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Animation speed: {light.AnimationSpeed}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"Animation intensity: {light.AnimationIntensity}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatBool(bool value) => value ? "yes" : "no";
}
