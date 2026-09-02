using System.Collections;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Brickwork.App.ViewModels;

namespace Brickwork.App.Views.Panels;

public partial class WallsToolView : UserControl
{
    private int _expandedForTreeRevision = -1;

    public WallsToolView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        WallsTree.PropertyChanged += OnWallsTreePropertyChanged;
        WallsTree.AddHandler(InputElement.PointerMovedEvent, OnTreePointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);
        WallsTree.AddHandler(InputElement.PointerPressedEvent, OnTreePointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || DataContext is not WallsToolViewModel viewModel)
        {
            return;
        }

        viewModel.ClearSelection();
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

    private void OnTreePointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is not WallsToolViewModel viewModel)
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
        if (!e.GetCurrentPoint(WallsTree).Properties.IsLeftButtonPressed ||
            DataContext is not WallsToolViewModel viewModel)
        {
            return;
        }

        var treeItem = ResolveTreeViewItem(WallsTree, e.GetPosition(WallsTree));
        switch (treeItem?.DataContext)
        {
            case WallItemViewModel wallItem:
                viewModel.ActivateTreeItem(wallItem);
                break;
            case WallPortalItemViewModel portalItem:
                viewModel.ActivateTreeItem(portalItem);
                break;
            default:
                viewModel.ClearSelection();
                break;
        }
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
