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
    private bool _marqueeActive;
    private bool _marqueeAddToSelection;
    private bool _marqueeArmed;

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
        if (DataContext is not MapPreviewDocumentViewModel viewModel)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            CancelMarquee();
            viewModel.ClearWallSelection();
            e.Handled = true;
            return;
        }

        if ((e.Key is Key.Delete or Key.Back) && e.KeyModifiers == KeyModifiers.None)
        {
            if (viewModel.DeleteSelectedWalls())
            {
                e.Handled = true;
            }
        }
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
        CancelMarquee();
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
            CancelMarquee();

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
            _marqueeActive = false;
            _marqueeAddToSelection = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

            _vertexDragActive = viewModel.TryBeginVertexDrag(ToPreviewPoint(pressPosition));
            if (_vertexDragActive)
            {
                e.Pointer.Capture(MapViewport);
                e.Handled = true;
                return;
            }

            // Marquee only from empty canvas (not on a wall/node).
            _marqueeArmed = viewModel.IsWallEditingToolActive
                && !viewModel.HasWallAt(ToPreviewPoint(pressPosition));
            if (_marqueeArmed)
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

        if (_marqueeArmed &&
            _leftPressPosition is { } press &&
            e.GetCurrentPoint(MapViewport).Properties.IsLeftButtonPressed)
        {
            var current = e.GetPosition(MapViewport);
            var delta = current - press;
            if (!_marqueeActive &&
                (Math.Abs(delta.X) > ClickMoveThreshold || Math.Abs(delta.Y) > ClickMoveThreshold))
            {
                _marqueeActive = true;
            }

            if (_marqueeActive)
            {
                UpdateMarqueeRect(press, current);
                e.Handled = true;
                return;
            }
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
                var previewPoint = ToPreviewPoint(e.GetPosition(MapViewport));
                if (!viewModel.TryRemoveVertexAt(previewPoint))
                {
                    viewModel.TryInsertVertexAt(previewPoint);
                }
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
        if (_marqueeActive && _leftPressPosition is { } marqueePress)
        {
            viewModel.ApplyMarqueeSelection(
                ToPreviewPoint(marqueePress),
                ToPreviewPoint(leftReleasePosition),
                _marqueeAddToSelection);
            CancelMarquee();
            if (e.Pointer.Captured == MapViewport)
            {
                e.Pointer.Capture(null);
            }

            e.Handled = true;
            _leftPressPosition = null;
            return;
        }

        if (_leftPressPosition is { } pressPosition)
        {
            var delta = leftReleasePosition - pressPosition;
            if (Math.Abs(delta.X) <= ClickMoveThreshold && Math.Abs(delta.Y) <= ClickMoveThreshold)
            {
                var shiftToggle = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
                viewModel.HandlePrimaryClick(ToPreviewPoint(leftReleasePosition), shiftToggle);
                e.Handled = true;
            }
        }

        CancelMarquee();
        if (e.Pointer.Captured == MapViewport)
        {
            e.Pointer.Capture(null);
        }

        _leftPressPosition = null;
    }

    private void UpdateMarqueeRect(Point origin, Point current)
    {
        var left = Math.Min(origin.X, current.X);
        var top = Math.Min(origin.Y, current.Y);
        var width = Math.Abs(current.X - origin.X);
        var height = Math.Abs(current.Y - origin.Y);
        Canvas.SetLeft(MarqueeRect, left);
        Canvas.SetTop(MarqueeRect, top);
        MarqueeRect.Width = width;
        MarqueeRect.Height = height;
        MarqueeRect.IsVisible = true;
    }

    private void CancelMarquee()
    {
        _marqueeActive = false;
        _marqueeArmed = false;
        _marqueeAddToSelection = false;
        MarqueeRect.IsVisible = false;
        MarqueeRect.Width = 0;
        MarqueeRect.Height = 0;
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
