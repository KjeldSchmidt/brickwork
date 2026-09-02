namespace Brickwork.Core.Models;

public static class WallLineTypeFormatting
{
    public static string ToDisplayName(this WallLineType lineType) =>
        lineType switch
        {
            WallLineType.SecretDoor => "Secret Door",
            _ => lineType.ToString(),
        };
}
