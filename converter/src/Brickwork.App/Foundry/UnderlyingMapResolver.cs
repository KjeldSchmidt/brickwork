using Brickwork.Core.Foundry;
using Brickwork.Core.Models;
using SkiaSharp;

namespace Brickwork.App.Foundry;

/// <summary>Resolves a Foundry background image under the maps folder and reads its pixel size.</summary>
public static class UnderlyingMapResolver
{
    public static UnderlyingMapInfo? TryResolve(string? mapsDirectory, string? sourceFileName)
    {
        var absolutePath = FoundryPaths.TryResolveBackgroundImagePath(mapsDirectory, sourceFileName);
        if (absolutePath is null)
        {
            return null;
        }

        try
        {
            using var image = SKImage.FromEncodedData(absolutePath);
            if (image is null || image.Width <= 0 || image.Height <= 0)
            {
                return null;
            }

            return new UnderlyingMapInfo
            {
                FileName = Path.GetFileName(absolutePath),
                AbsolutePath = absolutePath,
                Width = image.Width,
                Height = image.Height,
            };
        }
        catch
        {
            return null;
        }
    }
}
