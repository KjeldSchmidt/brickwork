using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using InkarnateTools.App.Rendering;
using InkarnateTools.Core.Models;
using SkiaSharp;

namespace InkarnateTools.App.Controls;

public class MapViewportControl : Control
{
    public static readonly StyledProperty<MapDocument?> MapProperty =
        AvaloniaProperty.Register<MapViewportControl, MapDocument?>(nameof(Map));

    public static readonly StyledProperty<int> ContentRevisionProperty =
        AvaloniaProperty.Register<MapViewportControl, int>(nameof(ContentRevision));

    private readonly IMapSceneRenderer _renderer = new MapSceneRenderer();

    static MapViewportControl()
    {
        AffectsRender<MapViewportControl>(MapProperty, ContentRevisionProperty);
    }

    public int ContentRevision
    {
        get => GetValue(ContentRevisionProperty);
        set => SetValue(ContentRevisionProperty, value);
    }

    public MapDocument? Map
    {
        get => GetValue(MapProperty);
        set => SetValue(MapProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Map is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            base.Render(context);
            return;
        }

        var bounds = new Rect(Bounds.Size);
        context.Custom(new MapDrawOperation(bounds, Map, _renderer));
    }

    private sealed class MapDrawOperation : ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly MapDocument _map;
        private readonly IMapSceneRenderer _renderer;

        public MapDrawOperation(Rect bounds, MapDocument map, IMapSceneRenderer renderer)
        {
            _bounds = bounds;
            _map = map;
            _renderer = renderer;
        }

        public Rect Bounds => _bounds;

        public void Dispose()
        {
        }

        public bool Equals(ICustomDrawOperation? other) => false;

        public bool HitTest(Point p) => _bounds.Contains(p);

        public void Render(ImmediateDrawingContext context)
        {
            if (context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) is not ISkiaSharpApiLeaseFeature leaseFeature)
            {
                return;
            }

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;
            if (canvas is null)
            {
                return;
            }

            canvas.Save();
            canvas.ClipRect(new SKRect(0, 0, (float)_bounds.Width, (float)_bounds.Height));

            var previewWidth = _map.Preview?.Width ?? (int)_bounds.Width;
            var previewHeight = _map.Preview?.Height ?? (int)_bounds.Height;
            var destination = new SKRect(0, 0, previewWidth, previewHeight);

            _renderer.Render(canvas, _map, destination);
            canvas.Restore();
        }
    }
}
