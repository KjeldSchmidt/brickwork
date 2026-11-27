using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
    private bool _entityMoveArmed;
    private bool _entityMoveActive;
    private bool _marqueeActive;
    private bool _marqueeAddToSelection;
    private bool _marqueeArmed;

    private Point? _middlePressPosition;
    private bool _middleDragMoved;
    private bool _middleArmedForPortal;
    private bool _portalResizeFromMiddle;
    private int _leftClickCount;
    private bool _leftStartedDrawingThisPress;
    private bool _drawingLeftClickActive;
    private bool _eraserStrokeActive;
    private bool _trunkOffsetDragActive;

    public MapPreviewDocumentView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        MapZoom.LayoutUpdated += OnMapZoomLayoutUpdated;
        MapViewport.AddHandler(InputElement.PointerPressedEvent, OnMapViewportPointerPressed, handledEventsToo: true);
        MapViewport.AddHandler(InputElement.PointerMovedEvent, OnMapViewportPointerMoved, handledEventsToo: true);
        MapViewport.AddHandler(InputElement.PointerReleasedEvent, OnMapViewportPointerReleased, handledEventsToo: true);
        MapViewport.AddHandler(InputElement.PointerExitedEvent, OnMapViewportPointerExited, handledEventsToo: true);
        // Tunnel so Shift+scroll on tree trunks is seen before the zoom border zooms or scrolls.
        MapZoom.AddHandler(InputElement.PointerWheelChangedEvent, OnMapPointerWheel, RoutingStrategies.Tunnel);
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
            if (_entityMoveActive)
            {
                viewModel.EndEntityMove();
                _entityMoveActive = false;
                _entityMoveArmed = false;
            }

            if (viewModel.IsDrawing)
            {
                viewModel.CancelDrawing();
            }
            else
            {
                viewModel.ClearSelection();
            }

            e.Handled = true;
            return;
        }

        if ((e.Key is Key.Enter or Key.Return) && e.KeyModifiers == KeyModifiers.None)
        {
            if (viewModel.IsDrawing && viewModel.TryFinishDrawing())
            {
                e.Handled = true;
            }

            return;
        }

        if ((e.Key is Key.Delete or Key.Back) && e.KeyModifiers == KeyModifiers.None)
        {
            if (viewModel.DeleteSelection())
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
        ResetMiddleState();
        ResetDrawingClickState();
        _eraserStrokeActive = false;
        ScheduleInitialFit();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MapPreviewDocumentViewModel.Map) or nameof(MapPreviewDocumentViewModel.HasMap))
        {
            _vertexDragActive = false;
            _entityMoveArmed = false;
            _entityMoveActive = false;
            _leftPressPosition = null;
            _rightPressScreenPosition = null;
            _rightDragMoved = false;
            CancelMarquee();
            ResetMiddleState();
            ResetDrawingClickState();
            _eraserStrokeActive = false;

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
            var previewPress = ToPreviewPoint(pressPosition);
            _leftPressPosition = pressPosition;
            _leftClickCount = e.ClickCount;
            _leftStartedDrawingThisPress = false;
            _drawingLeftClickActive = false;
            _rightPressScreenPosition = null;
            _rightDragMoved = false;
            _marqueeActive = false;
            _marqueeAddToSelection = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

            // While drawing: single-click places a vertex (on release); double-click finishes.
            if (viewModel.IsDrawingWall)
            {
                _drawingLeftClickActive = true;
                if (e.ClickCount >= 2)
                {
                    viewModel.TryFinishDrawingWall(previewPress);
                    _leftPressPosition = null;
                    _drawingLeftClickActive = false;
                }

                e.Pointer.Capture(MapViewport);
                e.Handled = true;
                return;
            }

            if (viewModel.IsDrawingRegion)
            {
                _drawingLeftClickActive = true;
                if (e.ClickCount >= 2)
                {
                    viewModel.TryFinishDrawingRegion(previewPress);
                    _leftPressPosition = null;
                    _drawingLeftClickActive = false;
                }

                e.Pointer.Capture(MapViewport);
                e.Handled = true;
                return;
            }

            // Double-click empty space starts a new wall/region.
            if (e.ClickCount >= 2)
            {
                if (viewModel.IsWallEditingToolActive &&
                    viewModel.TryStartDrawingWall(previewPress))
                {
                    _leftStartedDrawingThisPress = true;
                    _drawingLeftClickActive = true;
                    e.Pointer.Capture(MapViewport);
                    e.Handled = true;
                    return;
                }

                if (viewModel.IsRegionEditingToolActive &&
                    viewModel.TryStartDrawingRegion(previewPress))
                {
                    _leftStartedDrawingThisPress = true;
                    _drawingLeftClickActive = true;
                    e.Pointer.Capture(MapViewport);
                    e.Handled = true;
                    return;
                }
            }

            if (viewModel.IsEraserToolActive)
            {
                _eraserStrokeActive = viewModel.TryBeginEraserStroke(previewPress);
                if (_eraserStrokeActive)
                {
                    _leftPressPosition = null;
                    e.Pointer.Capture(MapViewport);
                    e.Handled = true;
                }

                return;
            }

            // Trees tab: Shift+drag on a selected trunk moves its version's trunk offset.
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && viewModel.TryBeginTrunkOffsetDrag(previewPress))
            {
                _trunkOffsetDragActive = true;
                _leftPressPosition = null;
                e.Pointer.Capture(MapViewport);
                e.Handled = true;
                return;
            }

            _vertexDragActive = viewModel.TryBeginVertexDrag(previewPress);
            if (_vertexDragActive)
            {
                e.Pointer.Capture(MapViewport);
                e.Handled = true;
                return;
            }

            _entityMoveArmed = viewModel.CanBeginEntityMoveAt(previewPress);
            if (_entityMoveArmed)
            {
                e.Pointer.Capture(MapViewport);
                e.Handled = true;
                return;
            }

            _marqueeArmed =
                (viewModel.IsWallEditingToolActive && !viewModel.HasWallAt(previewPress))
                || (viewModel.IsRegionEditingToolActive && !viewModel.HasRegionAt(previewPress));
            if (_marqueeArmed)
            {
                e.Pointer.Capture(MapViewport);
                e.Handled = true;
            }

            return;
        }

        if (properties.IsRightButtonPressed)
        {
            _rightPressScreenPosition = e.GetPosition(MapZoom);
            _rightDragMoved = false;
            return;
        }

        if (properties.IsMiddleButtonPressed)
        {
            // Middle-click is only used for portal create / resize while wall editing.
            if (!viewModel.IsWallEditingToolActive || viewModel.IsDrawing)
            {
                return;
            }

            var pressPosition = e.GetPosition(MapViewport);
            _middlePressPosition = pressPosition;
            _middleDragMoved = false;
            _portalResizeFromMiddle = false;
            _middleArmedForPortal = viewModel.HasWallAt(ToPreviewPoint(pressPosition));

            e.Pointer.Capture(MapViewport);
            e.Handled = true;
        }
    }

    private void OnMapViewportPointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is not MapPreviewDocumentViewModel { HasMap: true } viewModel)
        {
            return;
        }

        if (_portalResizeFromMiddle)
        {
            if (!e.GetCurrentPoint(MapViewport).Properties.IsMiddleButtonPressed)
            {
                return;
            }

            viewModel.DragVertexTo(ToPreviewPoint(e.GetPosition(MapViewport)));
            e.Handled = true;
            return;
        }

        if (_trunkOffsetDragActive)
        {
            if (e.GetCurrentPoint(MapViewport).Properties.IsLeftButtonPressed)
            {
                viewModel.DragTrunkOffsetTo(ToPreviewPoint(e.GetPosition(MapViewport)));
                e.Handled = true;
            }

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

        if (_entityMoveArmed &&
            _leftPressPosition is { } entityPress &&
            e.GetCurrentPoint(MapViewport).Properties.IsLeftButtonPressed)
        {
            var current = e.GetPosition(MapViewport);
            var delta = current - entityPress;
            if (!_entityMoveActive &&
                (Math.Abs(delta.X) > ClickMoveThreshold || Math.Abs(delta.Y) > ClickMoveThreshold))
            {
                _entityMoveActive = viewModel.TryBeginEntityMove(ToPreviewPoint(entityPress));
                if (!_entityMoveActive)
                {
                    _entityMoveArmed = false;
                }
            }

            if (_entityMoveActive)
            {
                viewModel.DragEntityTo(ToPreviewPoint(current));
                e.Handled = true;
                return;
            }
        }

        if (_eraserStrokeActive)
        {
            if (!e.GetCurrentPoint(MapViewport).Properties.IsLeftButtonPressed)
            {
                return;
            }

            viewModel.ContinueEraserStroke(ToPreviewPoint(e.GetPosition(MapViewport)));
            e.Handled = true;
            return;
        }

        if (_middlePressPosition is { } middlePress &&
            e.GetCurrentPoint(MapViewport).Properties.IsMiddleButtonPressed)
        {
            var current = e.GetPosition(MapViewport);
            var delta = current - middlePress;
            if (!_middleDragMoved &&
                (Math.Abs(delta.X) > ClickMoveThreshold || Math.Abs(delta.Y) > ClickMoveThreshold))
            {
                _middleDragMoved = true;
                if (_middleArmedForPortal)
                {
                    _portalResizeFromMiddle = viewModel.TryBeginPortalCreateDrag(ToPreviewPoint(middlePress));
                    _middleArmedForPortal = false;
                    if (_portalResizeFromMiddle)
                    {
                        viewModel.DragVertexTo(ToPreviewPoint(current));
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        if (viewModel.IsDrawingWall)
        {
            viewModel.UpdateDrawingWallPreview(ToPreviewPoint(e.GetPosition(MapViewport)));
        }
        else if (viewModel.IsDrawingRegion)
        {
            viewModel.UpdateDrawingRegionPreview(ToPreviewPoint(e.GetPosition(MapViewport)));
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
        MapViewport.Cursor = viewModel.HoveredWallEntityId is not null || viewModel.HoveredRegionEntityId is not null
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

        if (e.InitialPressMouseButton == MouseButton.Middle)
        {
            var releasePosition = e.GetPosition(MapViewport);
            if (_portalResizeFromMiddle)
            {
                viewModel.EndVertexDrag();
            }
            else if (_middleArmedForPortal && !_middleDragMoved)
            {
                viewModel.TryAddPortalAt(ToPreviewPoint(releasePosition));
            }

            ResetMiddleState();
            if (e.Pointer.Captured == MapViewport)
            {
                e.Pointer.Capture(null);
            }

            e.Handled = true;
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

            if (_rightPressScreenPosition is not null && !_rightDragMoved && !viewModel.IsDrawing)
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

        if (_drawingLeftClickActive || _leftStartedDrawingThisPress)
        {
            var releasePosition = e.GetPosition(MapViewport);
            var previewRelease = ToPreviewPoint(releasePosition);
            var noDrag = _leftPressPosition is { } drawingPress &&
                         Math.Abs(releasePosition.X - drawingPress.X) <= ClickMoveThreshold &&
                         Math.Abs(releasePosition.Y - drawingPress.Y) <= ClickMoveThreshold;

            if (_leftStartedDrawingThisPress)
            {
                // Double-click that started drawing — keep rubber-banding from the start point.
                if (viewModel.IsDrawingWall)
                {
                    viewModel.UpdateDrawingWallPreview(previewRelease);
                }
                else if (viewModel.IsDrawingRegion)
                {
                    viewModel.UpdateDrawingRegionPreview(previewRelease);
                }
            }
            else if (viewModel.IsDrawingWall && noDrag && _leftClickCount < 2)
            {
                viewModel.CommitDrawingWallVertex(previewRelease);
            }
            else if (viewModel.IsDrawingRegion && noDrag && _leftClickCount < 2)
            {
                viewModel.CommitDrawingRegionVertex(previewRelease);
            }
            else if (viewModel.IsDrawingWall)
            {
                viewModel.UpdateDrawingWallPreview(previewRelease);
            }
            else if (viewModel.IsDrawingRegion)
            {
                viewModel.UpdateDrawingRegionPreview(previewRelease);
            }

            _drawingLeftClickActive = false;
            _leftStartedDrawingThisPress = false;
            _leftClickCount = 0;
            _leftPressPosition = null;
            if (e.Pointer.Captured == MapViewport)
            {
                e.Pointer.Capture(null);
            }

            e.Handled = true;
            return;
        }

        if (_trunkOffsetDragActive)
        {
            viewModel.EndTrunkOffsetDrag();
            _trunkOffsetDragActive = false;
            if (e.Pointer.Captured == MapViewport)
            {
                e.Pointer.Capture(null);
            }

            e.Handled = true;
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

        if (_entityMoveActive)
        {
            viewModel.EndEntityMove();
            _entityMoveActive = false;
            _entityMoveArmed = false;
            _leftPressPosition = null;
            if (e.Pointer.Captured == MapViewport)
            {
                e.Pointer.Capture(null);
            }

            e.Handled = true;
            return;
        }

        _entityMoveArmed = false;

        if (_eraserStrokeActive)
        {
            viewModel.EndEraserStroke();
            _eraserStrokeActive = false;
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
                viewModel.HandlePrimaryClick(
                    ToPreviewPoint(leftReleasePosition),
                    shiftToggle,
                    isDoubleClick: _leftClickCount >= 2);
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

    private void OnMapPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift) ||
            DataContext is not MapPreviewDocumentViewModel { HasMap: true } viewModel)
        {
            return;
        }

        // macOS turns Shift+wheel into horizontal scrolling, so accept either axis.
        var notches = Math.Abs(e.Delta.Y) > Math.Abs(e.Delta.X) ? e.Delta.Y : e.Delta.X;
        if (viewModel.TryScaleSelectedTrunks(notches))
        {
            e.Handled = true;
        }
    }

    private void ResetMiddleState()
    {
        _middlePressPosition = null;
        _middleDragMoved = false;
        _middleArmedForPortal = false;
        _portalResizeFromMiddle = false;
    }

    private void ResetDrawingClickState()
    {
        _leftClickCount = 0;
        _leftStartedDrawingThisPress = false;
        _drawingLeftClickActive = false;
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
        _entityMoveArmed = false;
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
