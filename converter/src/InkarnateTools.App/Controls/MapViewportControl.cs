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

    public static readonly StyledProperty<int> HighlightRevisionProperty =
        AvaloniaProperty.Register<MapViewportControl, int>(nameof(HighlightRevision));

    public static readonly StyledProperty<int?> FocusedWallEntityIdProperty =
        AvaloniaProperty.Register<MapViewportControl, int?>(nameof(FocusedWallEntityId));

    public static readonly StyledProperty<WallPortal?> FocusedPortalProperty =
        AvaloniaProperty.Register<MapViewportControl, WallPortal?>(nameof(FocusedPortal));

    public static readonly StyledProperty<int?> HoveredWallEntityIdProperty =
        AvaloniaProperty.Register<MapViewportControl, int?>(nameof(HoveredWallEntityId));

    public static readonly StyledProperty<WallPortal?> HoveredPortalProperty =
        AvaloniaProperty.Register<MapViewportControl, WallPortal?>(nameof(HoveredPortal));

    private readonly IMapSceneRenderer _renderer = new MapSceneRenderer();

    static MapViewportControl()
    {
        AffectsRender<MapViewportControl>(
            MapProperty,
            ContentRevisionProperty,
            HighlightRevisionProperty,
            FocusedWallEntityIdProperty,
            FocusedPortalProperty,
            HoveredWallEntityIdProperty,
            HoveredPortalProperty);

        MapProperty.Changed.AddClassHandler<MapViewportControl>((control, args) =>
        {
            if (args.OldValue is MapDocument oldMap)
            {
                control._renderer.ReleaseMap(oldMap);
            }
        });
    }

    public int ContentRevision
    {
        get => GetValue(ContentRevisionProperty);
        set => SetValue(ContentRevisionProperty, value);
    }

    public int HighlightRevision
    {
        get => GetValue(HighlightRevisionProperty);
        set => SetValue(HighlightRevisionProperty, value);
    }

    public int? FocusedWallEntityId
    {
        get => GetValue(FocusedWallEntityIdProperty);
        set => SetValue(FocusedWallEntityIdProperty, value);
    }

    public WallPortal? FocusedPortal
    {
        get => GetValue(FocusedPortalProperty);
        set => SetValue(FocusedPortalProperty, value);
    }

    public int? HoveredWallEntityId
    {
        get => GetValue(HoveredWallEntityIdProperty);
        set => SetValue(HoveredWallEntityIdProperty, value);
    }

    public WallPortal? HoveredPortal
    {
        get => GetValue(HoveredPortalProperty);
        set => SetValue(HoveredPortalProperty, value);
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
        var highlight = new MapRenderHighlight(
            FocusedWallEntityId,
            FocusedPortal,
            HoveredWallEntityId,
            HoveredPortal);
        context.Custom(new MapDrawOperation(bounds, Map, _renderer, highlight));
    }

    private sealed class MapDrawOperation : ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly MapDocument _map;
        private readonly IMapSceneRenderer _renderer;
        private readonly MapRenderHighlight _highlight;

        public MapDrawOperation(
            Rect bounds,
            MapDocument map,
            IMapSceneRenderer renderer,
            MapRenderHighlight highlight)
        {
            _bounds = bounds;
            _map = map;
            _renderer = renderer;
            _highlight = highlight;
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

            _renderer.Render(canvas, _map, destination, _highlight);
            canvas.Restore();
        }
    }
}
