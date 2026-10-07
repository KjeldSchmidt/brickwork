using System.Collections;
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

public partial class WallsToolView : UserControl
{
    private readonly ContextMenu _treeContextMenu;
    private readonly MenuItem _renameMenuItem = new()
    {
        Header = "Rename",
        InputGesture = new KeyGesture(Key.F2),
    };
    private readonly MenuItem _convertMenuItem = new()
    {
        Header = "Convert to Region",
    };
    private readonly MenuItem _deleteMenuItem = new()
    {
        Header = "Delete",
        InputGesture = new KeyGesture(Key.Delete),
    };
    private object? _contextMenuTarget;
    private TopLevel? _topLevel;
    private int _expandedForTreeRevision = -1;
    // ComboBox popups sit above the tree; the closing click often falls through and
    // would otherwise select/hover whichever row is under the dropdown.
    private bool _suppressTreePointerInput;

    public WallsToolView()
    {
        InitializeComponent();
        ToolTip.SetTip(
            _convertMenuItem,
            "Replace this wall with a region. Open walls use the terrain-style thickness outline; closed walls use the closed path itself.");
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
        WallsTree.PropertyChanged += OnWallsTreePropertyChanged;
        WallsTree.AddHandler(InputElement.PointerMovedEvent, OnTreePointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);
        WallsTree.AddHandler(InputElement.PointerPressedEvent, OnTreePointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        WallsTree.AddHandler(InputElement.DoubleTappedEvent, OnTreeDoubleTapped, RoutingStrategies.Bubble, handledEventsToo: true);
        KeyDown += OnKeyDown;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _topLevel = TopLevel.GetTopLevel(this);
        // Catch right-clicks that only dismiss an open menu overlay and never reach the tree.
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
            DataContext is not WallsToolViewModel viewModel ||
            !e.GetCurrentPoint(WallsTree).Properties.IsRightButtonPressed)
        {
            return;
        }

        var position = e.GetPosition(WallsTree);
        if (position.X < 0 ||
            position.Y < 0 ||
            position.X > WallsTree.Bounds.Width ||
            position.Y > WallsTree.Bounds.Height)
        {
            return;
        }

        TryOpenTreeContextMenu(e, viewModel);
    }

    private void UpdateContextMenuHeaders(WallsToolViewModel viewModel, object target)
    {
        var count = viewModel.GetEditTargets(target).Count;
        var suffix = count > 1 ? $" ({count})" : string.Empty;
        _renameMenuItem.Header = viewModel.CanRename(target) ? $"Rename{suffix}" : "Rename";
        _renameMenuItem.IsEnabled = viewModel.CanRename(target);
        _convertMenuItem.IsEnabled = viewModel.CanConvertToRegion(target) && count == 1;
        _deleteMenuItem.Header = viewModel.CanDelete(target) ? $"Delete{suffix}" : "Delete";
        _deleteMenuItem.IsEnabled = viewModel.CanDelete(target);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not WallsToolViewModel viewModel)
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
            if (viewModel.DeleteSelectedWalls())
            {
                e.Handled = true;
            }
        }
    }

    private void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not WallsToolViewModel viewModel)
        {
            return;
        }

        if (_suppressTreePointerInput || IsTreeEditorInteraction(e.Source))
        {
            return;
        }

        var treeItem = ResolveTreeViewItem(WallsTree, e.GetPosition(WallsTree));
        if (!viewModel.CanRename(treeItem?.DataContext))
        {
            return;
        }

        viewModel.BeginRename(treeItem!.DataContext);
        ScheduleFocusRenameBox();
        e.Handled = true;
    }

    private void OnRenameMenuClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not WallsToolViewModel viewModel || !viewModel.CanRename(_contextMenuTarget))
        {
            return;
        }

        viewModel.BeginRename(_contextMenuTarget);
        ScheduleFocusRenameBox();
    }

    private void OnConvertMenuClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WallsToolViewModel viewModel)
        {
            viewModel.ConvertToRegion(_contextMenuTarget);
        }
    }

    private void OnDeleteMenuClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WallsToolViewModel viewModel)
        {
            viewModel.DeleteTreeItem(_contextMenuTarget);
        }
    }

    public bool TryBeginRenameFromHotkey()
    {
        if (DataContext is not WallsToolViewModel viewModel || !viewModel.BeginRenameSelection())
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
        var renameBox = WallsTree.GetVisualDescendants()
            .OfType<TextBox>()
            .FirstOrDefault(box => box.Classes.Contains("wall-tree-rename") && box.IsVisible);
        if (renameBox is null)
        {
            return;
        }

        renameBox.Focus();
        renameBox.SelectAll();
    }

    private void OnRenameBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || DataContext is not WallsToolViewModel viewModel)
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
        if (sender is TextBox textBox && DataContext is WallsToolViewModel viewModel)
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

        _expandedForTreeRevision = -1;
        ScheduleInitialExpandAll();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WallsToolViewModel.TreeRevision))
        {
            WallsTree.SelectedItem = null;
            ScheduleInitialExpandAll();
        }

        if (e.PropertyName is nameof(WallsToolViewModel.SelectedTreeItem))
        {
            ScheduleBringSelectionIntoView();
        }
    }

    private void OnWallsTreePropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TreeView.SelectedItemProperty)
        {
            ScheduleBringSelectionIntoView();
        }
    }

    private void OnWallTypeDropDownOpened(object? sender, EventArgs e)
    {
        _suppressTreePointerInput = true;
        if (DataContext is WallsToolViewModel viewModel)
        {
            viewModel.ClearTreeHover();
        }
    }

    private void OnWallTypeDropDownClosed(object? sender, EventArgs e)
    {
        // Keep suppression through the click that closed the popup (it often hits the tree).
        _suppressTreePointerInput = true;
        Dispatcher.UIThread.Post(
            () => _suppressTreePointerInput = false,
            DispatcherPriority.Input);
    }

    private void OnTreePointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is not WallsToolViewModel viewModel ||
            _suppressTreePointerInput ||
            IsTreeEditorInteraction(e.Source))
        {
            return;
        }

        var treeItem = ResolveTreeViewItem(WallsTree, e.GetPosition(WallsTree));
        switch (treeItem?.DataContext)
        {
            case WallItemViewModel wallItem:
                viewModel.SetHoveredTreeItem(wallItem);
                return;
            case WallPortalItemViewModel portalItem:
                viewModel.SetHoveredTreeItem(portalItem);
                return;
        }

        viewModel.ClearTreeHover();
    }

    private void OnTreePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not WallsToolViewModel viewModel)
        {
            return;
        }

        var point = e.GetCurrentPoint(WallsTree);
        if (point.Properties.IsRightButtonPressed)
        {
            TryOpenTreeContextMenu(e, viewModel);
            return;
        }

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        // Swallow fall-through clicks from a just-closed type dropdown so the TreeView
        // cannot replace the multi-selection with the row under the popup.
        if (_suppressTreePointerInput)
        {
            e.Handled = true;
            return;
        }

        // Let checkboxes, type editors, rename boxes, and expanders handle their own clicks.
        if (IsTreeEditorInteraction(e.Source))
        {
            return;
        }

        var treeItem = ResolveTreeViewItem(WallsTree, e.GetPosition(WallsTree));
        viewModel.HandleTreeActivation(treeItem?.DataContext, e.KeyModifiers);
        e.Handled = true;
    }

    private static bool IsTreeEditorInteraction(object? source)
    {
        if (source is CheckBox or ComboBox or TextBox or ToggleButton or Popup or PopupRoot)
        {
            return true;
        }

        return (source as Control)?.GetVisualAncestors().Any(ancestor =>
            ancestor is CheckBox or ComboBox or TextBox or ToggleButton or Popup or PopupRoot) == true;
    }

    private void TryOpenTreeContextMenu(PointerPressedEventArgs e, WallsToolViewModel viewModel)
    {
        // Let editors keep their own context menus / text selection.
        if (_suppressTreePointerInput || IsTreeEditorInteraction(e.Source))
        {
            return;
        }

        var treeItem = ResolveTreeViewItem(WallsTree, e.GetPosition(WallsTree));
        var target = treeItem?.DataContext;
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
        // Keep a multi-selection when right-clicking an already-selected node.
        if (viewModel.IsTreeNodeSelected(target))
        {
            viewModel.FocusTreeItem(target);
        }
        else
        {
            viewModel.HandleTreeActivation(target, e.KeyModifiers);
        }

        UpdateContextMenuHeaders(viewModel, target!);
        e.Handled = true;

        if (_treeContextMenu.IsOpen)
        {
            _treeContextMenu.Close();
        }

        // Open on the next pass so light-dismiss from an already-open menu doesn't eat this click.
        var host = treeItem as Control ?? WallsTree;
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

    private static TreeViewItem? ResolveTreeViewItem(TreeView tree, Point position)
    {
        if (tree.InputHitTest(position) is not Visual visual)
        {
            return null;
        }

        return visual.GetSelfAndVisualAncestors().OfType<TreeViewItem>().FirstOrDefault();
    }

    private void ScheduleInitialExpandAll()
    {
        if (DataContext is not WallsToolViewModel { HasLayers: true } viewModel)
        {
            return;
        }

        if (_expandedForTreeRevision == viewModel.TreeRevision)
        {
            return;
        }

        Dispatcher.UIThread.Post(
            () => TryInitialExpandAll(viewModel.TreeRevision),
            DispatcherPriority.Loaded);
    }

    private void ScheduleBringSelectionIntoView() =>
        Dispatcher.UIThread.Post(BringSelectionIntoView, DispatcherPriority.Background);

    private void TryInitialExpandAll(int treeRevision)
    {
        if (DataContext is not WallsToolViewModel { HasLayers: true } viewModel ||
            _expandedForTreeRevision == treeRevision)
        {
            return;
        }

        ExpandAllTreeItems();
        _expandedForTreeRevision = treeRevision;
    }

    /// <summary>
    /// Avalonia has no TreeView.ExpandAll. Nested containers are only created after a parent
    /// expands, so we expand depth-first and UpdateLayout so children exist before descending.
    /// </summary>
    private void ExpandAllTreeItems() => ExpandItemsControl(WallsTree);

    private void BringSelectionIntoView()
    {
        if (WallsTree.SelectedItem is null)
        {
            return;
        }

        var path = FindDataPath(WallsTree.ItemsSource, WallsTree.SelectedItem);
        if (path is not null)
        {
            ExpandDataPath(WallsTree, path, 0);
        }

        FindTreeViewItem(WallsTree, WallsTree.SelectedItem)?.BringIntoView();
    }

    private static void ExpandDataPath(ItemsControl parent, IReadOnlyList<object> path, int depth)
    {
        if (depth >= path.Count)
        {
            return;
        }

        parent.UpdateLayout();

        for (var index = 0; index < parent.ItemCount; index++)
        {
            if (parent.ContainerFromIndex(index) is not TreeViewItem item)
            {
                continue;
            }

            if (!ReferenceEquals(item.DataContext, path[depth]))
            {
                continue;
            }

            if (depth < path.Count - 1)
            {
                item.IsExpanded = true;
                item.UpdateLayout();
                ExpandDataPath(item, path, depth + 1);
            }

            return;
        }
    }

    private static List<object>? FindDataPath(IEnumerable? items, object target)
    {
        if (items is null)
        {
            return null;
        }

        foreach (var item in items)
        {
            if (ReferenceEquals(item, target) || Equals(item, target))
            {
                return [item];
            }

            var children = item switch
            {
                WallLayerNodeViewModel layer => layer.Children.Cast<object>(),
                WallGroupNodeViewModel group => group.Children.Cast<object>(),
                WallItemViewModel wall => wall.Portals.Cast<object>(),
                _ => null,
            };

            if (children is null)
            {
                continue;
            }

            var nestedPath = FindDataPath(children, target);
            if (nestedPath is null)
            {
                continue;
            }

            var path = new List<object> { item };
            path.AddRange(nestedPath);
            return path;
        }

        return null;
    }

    private static void ExpandItemsControl(ItemsControl itemsControl)
    {
        itemsControl.UpdateLayout();

        for (var index = 0; index < itemsControl.ItemCount; index++)
        {
            if (itemsControl.ContainerFromIndex(index) is not TreeViewItem item)
            {
                continue;
            }

            item.IsExpanded = true;
            item.UpdateLayout();
            ExpandItemsControl(item);
        }
    }

    private static TreeViewItem? FindTreeViewItem(ItemsControl parent, object dataItem)
    {
        parent.UpdateLayout();

        for (var index = 0; index < parent.ItemCount; index++)
        {
            if (parent.ContainerFromIndex(index) is not TreeViewItem item)
            {
                continue;
            }

            if (ReferenceEquals(item.DataContext, dataItem) || Equals(item.DataContext, dataItem))
            {
                return item;
            }

            var nested = FindTreeViewItem(item, dataItem);
            if (nested is not null)
            {
                return nested;
            }
        }

        foreach (var descendant in parent.GetVisualDescendants().OfType<TreeViewItem>())
        {
            if (ReferenceEquals(descendant.DataContext, dataItem) || Equals(descendant.DataContext, dataItem))
            {
                return descendant;
            }
        }

        return null;
    }

    private void OnTreePointerExited(object? sender, PointerEventArgs e)
    {
        if (DataContext is WallsToolViewModel viewModel)
        {
            viewModel.ClearTreeHover();
        }
    }
}
