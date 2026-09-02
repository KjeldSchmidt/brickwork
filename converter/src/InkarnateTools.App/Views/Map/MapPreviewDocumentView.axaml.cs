using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using InkarnateTools.App.ViewModels;
using InkarnateTools.Core.Models;

namespace InkarnateTools.App.Views.Map;

public partial class MapPreviewDocumentView : UserControl
{
    private const double ClickMoveThreshold = 4d;

    private MapDocument? _lastFittedMap;
    private Point? _leftPressPosition;
    private bool _vertexDragActive;

    public MapPreviewDocumentView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        MapZoom.LayoutUpdated += OnMapZoomLayoutUpdated;
        MapViewport.AddHandler(InputElement.PointerPressedEvent, OnMapViewportPointerPressed, handledEventsToo: true);
        MapViewport.AddHandler(InputElement.PointerMovedEvent, OnMapViewportPointerMoved, handledEventsToo: true);
        MapViewport.AddHandler(InputElement.PointerReleasedEvent, OnMapViewportPointerReleased, handledEventsToo: true);
        MapViewport.AddHandler(InputElement.PointerExitedEvent, OnMapViewportPointerExited, handledEventsToo: true);
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || DataContext is not MapPreviewDocumentViewModel viewModel)
        {
            return;
        }

        viewModel.ClearWallSelection();
        e.Handled = true;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is INotifyPropertyChanged oldContext)
        {
            oldContext.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (DataContext is INotifyPropertyChanged newContext)
        {
            newContext.PropertyChanged += OnViewModelPropertyChanged;
        }

        _lastFittedMap = null;
        ScheduleInitialFit();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MapPreviewDocumentViewModel.Map) or nameof(MapPreviewDocumentViewModel.HasMap))
        {
            _vertexDragActive = false;
            _leftPressPosition = null;

            if (sender is MapPreviewDocumentViewModel { HasMap: false })
            {
                _lastFittedMap = null;
                return;
            }

            _lastFittedMap = null;
            ScheduleInitialFit();
        }
    }

    private void OnMapZoomLayoutUpdated(object? sender, EventArgs e) => TryInitialFit();

    private void OnMapViewportPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MapPreviewDocumentViewModel { HasMap: true } viewModel)
        {
            return;
        }

        var properties = e.GetCurrentPoint(MapViewport).Properties;
        if (properties.IsLeftButtonPressed)
        {
            var pressPosition = e.GetPosition(MapViewport);
            _leftPressPosition = pressPosition;
            _vertexDragActive = viewModel.TryBeginVertexDrag(ToPreviewPoint(pressPosition));
            if (_vertexDragActive)
            {
                e.Pointer.Capture(MapViewport);
                e.Handled = true;
            }

            return;
        }

        if (properties.IsMiddleButtonPressed)
        {
            var previewPoint = ToPreviewPoint(e.GetPosition(MapViewport));
            viewModel.EditWallAt(previewPoint, cycleType: false, toggleActive: true);
            e.Handled = true;
        }
    }

    private void OnMapViewportPointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is not MapPreviewDocumentViewModel { HasMap: true } viewModel)
        {
            return;
        }

        if (_vertexDragActive)
        {
            if (!e.GetCurrentPoint(MapViewport).Properties.IsLeftButtonPressed)
            {
                return;
            }

            viewModel.DragVertexTo(ToPreviewPoint(e.GetPosition(MapViewport)));
            e.Handled = true;
            return;
        }

        viewModel.UpdateHoverAt(ToPreviewPoint(e.GetPosition(MapViewport)));
        MapViewport.Cursor = viewModel.HoveredWallEntityId is not null
            ? new Cursor(StandardCursorType.Hand)
            : Cursor.Default;
    }

    private void OnMapViewportPointerExited(object? sender, PointerEventArgs e)
    {
        if (DataContext is MapPreviewDocumentViewModel viewModel)
        {
            viewModel.ClearHover();
        }

        MapViewport.Cursor = Cursor.Default;
    }

    private void OnMapViewportPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is not MapPreviewDocumentViewModel { HasMap: true } viewModel)
        {
            return;
        }

        if (e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }

        if (_vertexDragActive)
        {
            viewModel.EndVertexDrag();
            _vertexDragActive = false;
            _leftPressPosition = null;
            if (e.Pointer.Captured == MapViewport)
            {
                e.Pointer.Capture(null);
            }

            e.Handled = true;
            return;
        }

        var releasePosition = e.GetPosition(MapViewport);
        if (_leftPressPosition is { } pressPosition)
        {
            var delta = releasePosition - pressPosition;
            if (Math.Abs(delta.X) <= ClickMoveThreshold && Math.Abs(delta.Y) <= ClickMoveThreshold)
            {
                var previewPoint = ToPreviewPoint(releasePosition);
                if (!viewModel.HasWallAt(previewPoint))
                {
                    viewModel.ClearWallSelection();
                    e.Handled = true;
                }
                else
                {
                    viewModel.EditWallAt(previewPoint, cycleType: true, toggleActive: false);
                    e.Handled = true;
                }
            }
        }

        _leftPressPosition = null;
    }

    private static MapPoint ToPreviewPoint(Point viewportPoint) =>
        new(viewportPoint.X, viewportPoint.Y);

    private void ScheduleInitialFit()
    {
        Dispatcher.UIThread.Post(TryInitialFit, DispatcherPriority.Loaded);
    }

    private void TryInitialFit()
    {
        if (DataContext is not MapPreviewDocumentViewModel { HasMap: true, Map: { } map })
        {
            return;
        }

        if (ReferenceEquals(map, _lastFittedMap))
        {
            return;
        }

        if (MapZoom.Bounds.Width <= 0 || MapZoom.Bounds.Height <= 0)
        {
            return;
        }

        MapZoom.Uniform();
        MapZoom.Focus();
        _lastFittedMap = map;
    }
}
