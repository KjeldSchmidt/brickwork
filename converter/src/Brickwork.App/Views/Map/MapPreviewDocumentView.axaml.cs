using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Brickwork.App.ViewModels;
using Brickwork.Core.Models;

namespace Brickwork.App.Views.Map;

public partial class MapPreviewDocumentView : UserControl
{
    private const double ClickMoveThreshold = 4d;

    private MapDocument? _lastFittedMap;
    private Point? _leftPressPosition;
    private Point? _rightPressScreenPosition;
    private bool _rightDragMoved;
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
            _rightPressScreenPosition = null;
            _rightDragMoved = false;

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
            _rightPressScreenPosition = null;
            _rightDragMoved = false;
            _vertexDragActive = viewModel.TryBeginVertexDrag(ToPreviewPoint(pressPosition));
            if (_vertexDragActive)
            {
                e.Pointer.Capture(MapViewport);
                e.Handled = true;
            }

            return;
        }

        if (properties.IsRightButtonPressed)
        {
            // Track in ZoomBorder space: pan keeps MapViewport-local coords almost fixed.
            // Do not mark Handled — ZoomBorder still needs the event for right-drag pan.
            _rightPressScreenPosition = e.GetPosition(MapZoom);
            _rightDragMoved = false;
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

        if (_rightPressScreenPosition is { } rightPress &&
            e.GetCurrentPoint(MapViewport).Properties.IsRightButtonPressed &&
            !_rightDragMoved)
        {
            var current = e.GetPosition(MapZoom);
            var delta = current - rightPress;
            if (Math.Abs(delta.X) > ClickMoveThreshold || Math.Abs(delta.Y) > ClickMoveThreshold)
            {
                _rightDragMoved = true;
            }
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

        if (e.InitialPressMouseButton == MouseButton.Right)
        {
            if (_rightPressScreenPosition is { } rightPress)
            {
                var releaseScreen = e.GetPosition(MapZoom);
                var delta = releaseScreen - rightPress;
                if (Math.Abs(delta.X) > ClickMoveThreshold || Math.Abs(delta.Y) > ClickMoveThreshold)
                {
                    _rightDragMoved = true;
                }
            }

            if (_rightPressScreenPosition is not null && !_rightDragMoved)
            {
                viewModel.TryInsertVertexAt(ToPreviewPoint(e.GetPosition(MapViewport)));
            }

            _rightPressScreenPosition = null;
            _rightDragMoved = false;
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

        var leftReleasePosition = e.GetPosition(MapViewport);
        if (_leftPressPosition is { } pressPosition)
        {
            var delta = leftReleasePosition - pressPosition;
            if (Math.Abs(delta.X) <= ClickMoveThreshold && Math.Abs(delta.Y) <= ClickMoveThreshold)
            {
                viewModel.HandlePrimaryClick(ToPreviewPoint(leftReleasePosition));
                e.Handled = true;
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
