using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Brickwork.App.ViewModels;

namespace Brickwork.App.Views.Panels;

public partial class LightsToolView : UserControl
{
    private const double DragThreshold = 6;

    private Point _pressPoint;
    private PointerPressedEventArgs? _pressArgs;
    private LightItemViewModel? _pressLight;
    private bool _pressIsDragCandidate;
    private bool _dragStarted;
    private LightKindViewModel? _dropTargetKind;

    public LightsToolView()
    {
        InitializeComponent();
        LightsTree.AddHandler(
            InputElement.DoubleTappedEvent,
            OnTreeDoubleTapped,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
    }

    private void OnSettingsHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
            sender is Control { DataContext: LightKindViewModel kind })
        {
            kind.IsSettingsExpanded = !kind.IsSettingsExpanded;
            e.Handled = true;
        }
    }

    private void OnInstancesHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
            sender is Control { DataContext: LightKindViewModel kind })
        {
            kind.IsInstancesExpanded = !kind.IsInstancesExpanded;
            e.Handled = true;
        }
    }

    private void OnLightPointerEntered(object? sender, PointerEventArgs e)
    {
        if (DataContext is LightsToolViewModel viewModel && sender is Control { DataContext: LightItemViewModel item })
        {
            viewModel.SetHoveredItem(item);
        }
    }

    private void OnTreePointerExited(object? sender, PointerEventArgs e)
    {
        if (DataContext is LightsToolViewModel viewModel)
        {
            viewModel.SetHoveredItem(null);
        }
    }

    private void OnLightPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not LightsToolViewModel ||
            sender is not Control { DataContext: LightItemViewModel light } ||
            IsEditorInteraction(e.Source))
        {
            return;
        }

        var point = e.GetCurrentPoint(LightsTree);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _pressPoint = point.Position;
        _pressArgs = e;
        _pressLight = light;
        _pressIsDragCandidate = true;
        _dragStarted = false;
    }

    private async void OnTreePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_pressIsDragCandidate ||
            _dragStarted ||
            _pressLight is null ||
            _pressArgs is null ||
            DataContext is not LightsToolViewModel viewModel)
        {
            return;
        }

        var point = e.GetCurrentPoint(LightsTree);
        if (!point.Properties.IsLeftButtonPressed)
        {
            ClearPressState();
            return;
        }

        var delta = point.Position - _pressPoint;
        if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold)
        {
            return;
        }

        _dragStarted = true;
        _pressIsDragCandidate = false;
        var ids = viewModel.GetLightsToMove(_pressLight);
        var data = new DataTransfer();
        data.Add(DataTransferItem.CreateText(EncodeLightIds(ids)));
        try
        {
            await DragDrop.DoDragDropAsync(_pressArgs, data, DragDropEffects.Move);
        }
        finally
        {
            ClearDropTarget();
            ClearPressState();
        }
    }

    private void OnTreePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragStarted)
        {
            ClearPressState();
            return;
        }

        if (DataContext is not LightsToolViewModel viewModel ||
            e.InitialPressMouseButton != MouseButton.Left ||
            IsEditorInteraction(e.Source))
        {
            ClearPressState();
            return;
        }

        var treeItem = ResolveTreeViewItem(LightsTree, e.GetPosition(LightsTree));
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (treeItem?.DataContext is LightItemViewModel light)
        {
            viewModel.SelectLight(light, ctrl, shift);
            e.Handled = true;
        }

        ClearPressState();
    }

    private void OnTreePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not LightsToolViewModel viewModel)
        {
            return;
        }

        var point = e.GetCurrentPoint(LightsTree);
        if (!point.Properties.IsLeftButtonPressed || IsEditorInteraction(e.Source))
        {
            return;
        }

        var treeItem = ResolveTreeViewItem(LightsTree, e.GetPosition(LightsTree));
        if (treeItem?.DataContext is LightItemViewModel)
        {
            // Selection deferred to release so a drag can cancel the click.
            return;
        }

        if (treeItem?.DataContext is LightKindViewModel kind)
        {
            viewModel.SelectKind(kind);
            e.Handled = true;
        }
    }

    private void OnTreePointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => ClearPressState();

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not LightsToolViewModel viewModel)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            viewModel.CancelRename();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F2 && e.KeyModifiers == KeyModifiers.None)
        {
            if (viewModel.BeginRenameSelection())
            {
                ScheduleFocusRenameBox();
                e.Handled = true;
            }
        }
    }

    private void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not LightsToolViewModel viewModel || IsEditorInteraction(e.Source))
        {
            return;
        }

        var treeItem = ResolveTreeViewItem(LightsTree, e.GetPosition(LightsTree));
        if (!viewModel.CanRename(treeItem?.DataContext))
        {
            return;
        }

        viewModel.BeginRename(treeItem!.DataContext);
        ScheduleFocusRenameBox();
        e.Handled = true;
    }

    private void OnRenameBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || DataContext is not LightsToolViewModel viewModel)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            viewModel.CommitRename(textBox.DataContext);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            viewModel.CancelRename();
            e.Handled = true;
        }
    }

    private void OnRenameBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && DataContext is LightsToolViewModel viewModel)
        {
            viewModel.CommitRename(textBox.DataContext);
        }
    }

    private void OnNewLightGroupClick(object? sender, RoutedEventArgs e) => ScheduleFocusRenameBox();

    private void ScheduleFocusRenameBox() =>
        Dispatcher.UIThread.Post(FocusActiveRenameBox, DispatcherPriority.Loaded);

    private void FocusActiveRenameBox()
    {
        var renameBox = LightsTree.GetVisualDescendants()
            .OfType<TextBox>()
            .FirstOrDefault(box => box.Classes.Contains("light-tree-rename") && box.IsVisible);
        if (renameBox is null)
        {
            return;
        }

        renameBox.Focus();
        renameBox.SelectAll();
    }

    private void OnKindDragOver(object? sender, DragEventArgs e)
    {
        if (!TryResolveDropKind(sender, out var kind) || !TryReadLightIds(e.DataTransfer, out _))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        SetDropTarget(kind);
        e.Handled = true;
    }

    private void OnKindDragLeave(object? sender, DragEventArgs e)
    {
        if (TryResolveDropKind(sender, out var kind) && ReferenceEquals(_dropTargetKind, kind))
        {
            ClearDropTarget();
        }
    }

    private void OnKindDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not LightsToolViewModel viewModel ||
            !TryResolveDropKind(sender, out var kind) ||
            !TryReadLightIds(e.DataTransfer, out var ids))
        {
            ClearDropTarget();
            return;
        }

        viewModel.MoveLightsToKind(ids, kind);
        ClearDropTarget();
        e.Handled = true;
    }

    private void OnLightDragOver(object? sender, DragEventArgs e)
    {
        if (sender is not Control { DataContext: LightItemViewModel light } ||
            !TryReadLightIds(e.DataTransfer, out _))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        SetDropTarget(light.Kind);
        e.Handled = true;
    }

    private void OnLightDragLeave(object? sender, DragEventArgs e)
    {
        if (sender is Control { DataContext: LightItemViewModel light } &&
            ReferenceEquals(_dropTargetKind, light.Kind))
        {
            ClearDropTarget();
        }
    }

    private void OnLightDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not LightsToolViewModel viewModel ||
            sender is not Control { DataContext: LightItemViewModel light } ||
            !TryReadLightIds(e.DataTransfer, out var ids))
        {
            ClearDropTarget();
            return;
        }

        viewModel.MoveLightsToKind(ids, light.Kind);
        ClearDropTarget();
        e.Handled = true;
    }

    private static bool TryResolveDropKind(object? sender, out LightKindViewModel kind)
    {
        kind = null!;
        if (sender is Control { DataContext: LightKindViewModel kindVm })
        {
            kind = kindVm;
            return true;
        }

        if (sender is Control { DataContext: LightItemViewModel light })
        {
            kind = light.Kind;
            return true;
        }

        return false;
    }

    private void SetDropTarget(LightKindViewModel kind)
    {
        if (ReferenceEquals(_dropTargetKind, kind))
        {
            return;
        }

        if (_dropTargetKind is not null)
        {
            _dropTargetKind.IsDropTarget = false;
        }

        _dropTargetKind = kind;
        kind.IsDropTarget = true;
    }

    private void ClearDropTarget()
    {
        if (_dropTargetKind is not null)
        {
            _dropTargetKind.IsDropTarget = false;
            _dropTargetKind = null;
        }
    }

    private void ClearPressState()
    {
        _pressLight = null;
        _pressArgs = null;
        _pressIsDragCandidate = false;
        _dragStarted = false;
    }

    private static string EncodeLightIds(IReadOnlyList<int> ids) =>
        $"{LightsToolViewModel.LightDragFormat}:{string.Join(',', ids)}";

    private static bool TryReadLightIds(IDataTransfer? transfer, out IReadOnlyList<int> ids)
    {
        ids = [];
        var text = transfer?.TryGetText();
        if (string.IsNullOrWhiteSpace(text) ||
            !text.StartsWith(LightsToolViewModel.LightDragFormat + ":", StringComparison.Ordinal))
        {
            return false;
        }

        var payload = text[(LightsToolViewModel.LightDragFormat.Length + 1)..];
        var parsed = payload
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var id) ? id : (int?)null)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .ToList();
        if (parsed.Count == 0)
        {
            return false;
        }

        ids = parsed;
        return true;
    }

    private static bool IsEditorInteraction(object? source)
    {
        if (source is CheckBox or ComboBox or TextBox or NumericUpDown or Slider or Button or ToggleButton or Popup or PopupRoot)
        {
            return true;
        }

        return (source as Control)?.GetVisualAncestors().Any(ancestor =>
            ancestor is CheckBox or ComboBox or TextBox or NumericUpDown or Slider or Button or ToggleButton or Popup or PopupRoot) == true;
    }

    private static TreeViewItem? ResolveTreeViewItem(TreeView tree, Point position)
    {
        if (tree.InputHitTest(position) is not Visual visual)
        {
            return null;
        }

        return visual.GetSelfAndVisualAncestors().OfType<TreeViewItem>().FirstOrDefault();
    }
}
