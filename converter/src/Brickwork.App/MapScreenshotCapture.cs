using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Brickwork.App.Controls;

namespace Brickwork.App;

public static class MapScreenshotCapture
{
    public static Task<string?> SaveViewportScreenshotAsync(MapViewportControl viewport) =>
        SaveViewportScreenshotAsync(
            viewport,
            Path.Combine(Path.GetTempPath(), $"brickwork-map-{DateTime.UtcNow:yyyyMMdd-HHmmss}.png"));

    public static async Task<string?> SaveViewportScreenshotAsync(MapViewportControl viewport, string outputPath)
    {
        if (viewport.Map is null || viewport.Bounds.Width <= 0 || viewport.Bounds.Height <= 0)
        {
            return null;
        }

        var pixelSize = new PixelSize(
            Math.Max(1, (int)Math.Round(viewport.Bounds.Width)),
            Math.Max(1, (int)Math.Round(viewport.Bounds.Height)));

        var renderTarget = new RenderTargetBitmap(pixelSize, new Vector(96, 96));
        renderTarget.Render(viewport);

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(outputPath);
        renderTarget.Save(stream);
        return outputPath;
    }

    public static MapViewportControl? FindMainMapViewport()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return null;
        }

        return desktop.MainWindow?.GetVisualDescendants().OfType<MapViewportControl>().FirstOrDefault();
    }
}
