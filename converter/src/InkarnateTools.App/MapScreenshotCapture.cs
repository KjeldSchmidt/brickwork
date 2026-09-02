using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using InkarnateTools.App.Controls;

namespace InkarnateTools.App;

public static class MapScreenshotCapture
{
    public static async Task<string?> SaveViewportScreenshotAsync(MapViewportControl viewport)
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

        var path = Path.Combine(
            Path.GetTempPath(),
            $"inkarnate-tools-map-{DateTime.UtcNow:yyyyMMdd-HHmmss}.png");

        await using var stream = File.OpenWrite(path);
        renderTarget.Save(stream);
        return path;
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
