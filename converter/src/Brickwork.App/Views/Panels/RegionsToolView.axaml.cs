using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Brickwork.App.ViewModels;

namespace Brickwork.App.Views.Panels;

public partial class RegionsToolView : UserControl
{
    private readonly ContextMenu _treeContextMenu;
    private readonly MenuItem _renameMenuItem = new()
    {
        Header = "Rename",
        InputGesture = new KeyGesture(Key.F2),
    };
    private readonly MenuItem _convertMenuItem = new()
    {
        Header = "Convert to Wall",
    };
    private readonly MenuItem _deleteMenuItem = new()
    {
        Header = "Delete",
        InputGesture = new KeyGesture(Key.Delete),
    };
    private object? _contextMenuTarget;
    private TopLevel? _topLevel;
    private bool _suppressListPointerInput;

    public RegionsToolView()
    {
        InitializeComponent();
        ToolTip.SetTip(
            _convertMenuItem,
            "Replace this region with a closed-path wall using the same boundary.");
        _renameMenuItem.Click += OnRenameMenuClick;
        _convertMenuItem.Click += OnConvertMenuClick;
        _deleteMenuItem.Click += OnDeleteMenuClick;
        _treeContextMenu = new ContextMenu
        {
            Items = { _renameMenuItem, _convertMenuItem, _deleteMenuItem },
        };
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        RegionsList.AddHandler(InputElement.PointerMovedEvent, OnListPointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);
        RegionsList.AddHandler(InputElement.PointerPressedEvent, OnListPointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        RegionsList.AddHandler(InputElement.DoubleTappedEvent, OnListDoubleTapped, RoutingStrategies.Bubble, handledEventsToo: true);
        KeyDown += OnKeyDown;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(
            InputElement.PointerPressedEvent,
            OnTopLevelPointerPressed,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _topLevel?.RemoveHandler(InputElement.PointerPressedEvent, OnTopLevelPointerPressed);
        _topLevel = null;
    }

    private void OnTopLevelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_treeContextMenu.IsOpen ||
            DataContext is not RegionsToolViewModel viewModel ||
            !e.GetCurrentPoint(RegionsList).Properties.IsRightButtonPressed)
        {
            return;
        }

        var position = e.GetPosition(RegionsList);
        if (position.X < 0 ||
            position.Y < 0 ||
            position.X > RegionsList.Bounds.Width ||
            position.Y > RegionsList.Bounds.Height)
        {
            return;
        }

        TryOpenContextMenu(e, viewModel);
    }

    private void UpdateContextMenuHeaders(RegionsToolViewModel viewModel, object target)
    {
        var count = viewModel.GetEditTargets(target).Count;
        var suffix = count > 1 ? $" ({count})" : string.Empty;
        _renameMenuItem.Header = viewModel.CanRename(target) ? $"Rename{suffix}" : "Rename";
        _renameMenuItem.IsEnabled = viewModel.CanRename(target);
        _convertMenuItem.IsEnabled = viewModel.CanConvertToWall(target) && count == 1;
        _deleteMenuItem.Header = viewModel.CanDelete(target) ? $"Delete{suffix}" : "Delete";
        _deleteMenuItem.IsEnabled = viewModel.CanDelete(target);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not RegionsToolViewModel viewModel)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            viewModel.CancelRename();
            viewModel.ClearSelection();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F2 && e.KeyModifiers == KeyModifiers.None)
        {
            if (TryBeginRenameFromHotkey())
            {
                e.Handled = true;
            }

            return;
        }

        if ((e.Key is Key.Delete or Key.Back) && e.KeyModifiers == KeyModifiers.None)
        {
            if (viewModel.DeleteSelectedRegions())
            {
                e.Handled = true;
            }
        }
    }

    private void OnRenameMenuClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not RegionsToolViewModel viewModel || !viewModel.CanRename(_contextMenuTarget))
        {
            return;
        }

        viewModel.BeginRename(_contextMenuTarget);
        ScheduleFocusRenameBox();
    }

    private void OnConvertMenuClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is RegionsToolViewModel viewModel)
        {
            viewModel.ConvertToWall(_contextMenuTarget);
        }
    }

    private void OnDeleteMenuClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is RegionsToolViewModel viewModel)
        {
            viewModel.DeleteTreeItem(_contextMenuTarget);
        }
    }

    public bool TryBeginRenameFromHotkey()
    {
        if (DataContext is not RegionsToolViewModel viewModel || !viewModel.BeginRenameSelection())
        {
            return false;
        }

        ScheduleFocusRenameBox();
        return true;
    }

    private void ScheduleFocusRenameBox() =>
        Dispatcher.UIThread.Post(FocusActiveRenameBox, DispatcherPriority.Loaded);

    private void FocusActiveRenameBox()
    {
        var renameBox = RegionsList.GetVisualDescendants()
            .OfType<TextBox>()
            .FirstOrDefault(box => box.Classes.Contains("wall-tree-rename") && box.IsVisible);
        renameBox?.Focus();
        renameBox?.SelectAll();
    }

    private void OnRenameBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || DataContext is not RegionsToolViewModel viewModel)
        {
            return;
        }

        if (e.Key is Key.Enter or Key.Return)
        {
            viewModel.CommitRename(textBox.DataContext);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            viewModel.CancelRename();
            e.Handled = true;
        }
    }

    private void OnRenameBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && DataContext is RegionsToolViewModel viewModel)
        {
            viewModel.CommitRename(textBox.DataContext);
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
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RegionsToolViewModel.TreeRevision)
            or nameof(RegionsToolViewModel.SelectedTreeItem))
        {
            if (DataContext is RegionsToolViewModel { SelectedTreeItem: { } selected })
            {
                RegionsList.ScrollIntoView(selected);
            }
        }
    }

    private void OnListPointerExited(object? sender, PointerEventArgs e)
    {
        if (DataContext is RegionsToolViewModel viewModel)
        {
            viewModel.ClearTreeHover();
        }
    }

    private void OnListPointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is not RegionsToolViewModel viewModel || _suppressListPointerInput)
        {
            return;
        }

        var item = ResolveListBoxItem(RegionsList, e.GetPosition(RegionsList));
        viewModel.SetHoveredTreeItem(item?.DataContext);
    }

    private void OnListPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not RegionsToolViewModel viewModel)
        {
            return;
        }

        var point = e.GetCurrentPoint(RegionsList);
        if (point.Properties.IsRightButtonPressed)
        {
            TryOpenContextMenu(e, viewModel);
            return;
        }

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (_suppressListPointerInput)
        {
            e.Handled = true;
            return;
        }

        if (IsEditorInteraction(e.Source))
        {
            return;
        }

        var item = ResolveListBoxItem(RegionsList, e.GetPosition(RegionsList));
        if (item?.DataContext is RegionItemViewModel region)
        {
            viewModel.SelectRegion(region, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            e.Handled = true;
        }
    }

    private void OnListDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not RegionsToolViewModel viewModel || IsEditorInteraction(e.Source))
        {
            return;
        }

        var item = ResolveListBoxItem(RegionsList, e.GetPosition(RegionsList));
        if (!viewModel.CanRename(item?.DataContext))
        {
            return;
        }

        viewModel.BeginRename(item!.DataContext);
        ScheduleFocusRenameBox();
        e.Handled = true;
    }

    private void TryOpenContextMenu(PointerPressedEventArgs e, RegionsToolViewModel viewModel)
    {
        if (_suppressListPointerInput || IsEditorInteraction(e.Source))
        {
            return;
        }

        var listItem = ResolveListBoxItem(RegionsList, e.GetPosition(RegionsList));
        var target = listItem?.DataContext;
        if (!viewModel.CanRename(target) && !viewModel.CanDelete(target))
        {
            if (_treeContextMenu.IsOpen)
            {
                _treeContextMenu.Close();
            }

            _contextMenuTarget = null;
            return;
        }

        _contextMenuTarget = target;
        if (viewModel.IsTreeNodeSelected(target))
        {
            viewModel.FocusTreeItem(target);
        }
        else if (target is RegionItemViewModel region)
        {
            viewModel.SelectRegion(region, addToSelection: false);
        }

        UpdateContextMenuHeaders(viewModel, target!);
        e.Handled = true;

        if (_treeContextMenu.IsOpen)
        {
            _treeContextMenu.Close();
        }

        var host = listItem as Control ?? RegionsList;
        Dispatcher.UIThread.Post(
            () =>
            {
                if (_contextMenuTarget is null)
                {
                    return;
                }

                UpdateContextMenuHeaders(viewModel, _contextMenuTarget);
                _treeContextMenu.Open(host);
            },
            DispatcherPriority.Input);
    }

    private void OnTypeDropDownOpened(object? sender, EventArgs e) => _suppressListPointerInput = true;

    private void OnTypeDropDownClosed(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() => _suppressListPointerInput = false, DispatcherPriority.Input);

    private static ListBoxItem? ResolveListBoxItem(ListBox list, Point position)
    {
        if (list.InputHitTest(position) is not Visual visual)
        {
            return null;
        }

        return visual.GetSelfAndVisualAncestors().OfType<ListBoxItem>().FirstOrDefault();
    }

    private static bool IsEditorInteraction(object? source) =>
        source is Visual visual &&
        visual.GetSelfAndVisualAncestors().Any(ancestor =>
            ancestor is CheckBox or ComboBox or TextBox or Thumb);
}
