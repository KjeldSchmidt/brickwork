using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using InkarnateTools.App.ViewModels;

namespace InkarnateTools.App.Views.Panels;

public partial class WallsToolView : UserControl
{
    public WallsToolView()
    {
        InitializeComponent();
        Loaded += (_, _) => ScheduleExpandAll();
        DataContextChanged += OnDataContextChanged;
        WallsTree.PropertyChanged += OnWallsTreePropertyChanged;
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

        ScheduleExpandAll();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WallsToolViewModel.Layers) or nameof(WallsToolViewModel.HasLayers))
        {
            ScheduleExpandAll();
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

    private void ScheduleExpandAll() =>
        Dispatcher.UIThread.Post(ExpandAllTreeItems, DispatcherPriority.Loaded);

    private void ScheduleBringSelectionIntoView() =>
        Dispatcher.UIThread.Post(BringSelectionIntoView, DispatcherPriority.Background);

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

        ExpandAllTreeItems();
        var container = FindTreeViewItem(WallsTree, WallsTree.SelectedItem);
        container?.BringIntoView();
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

        // Data templates wrap content; fall back to visual-tree DataContext search.
        foreach (var descendant in parent.GetVisualDescendants().OfType<TreeViewItem>())
        {
            if (ReferenceEquals(descendant.DataContext, dataItem) || Equals(descendant.DataContext, dataItem))
            {
                return descendant;
            }
        }

        return null;
    }
}
