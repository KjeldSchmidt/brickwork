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
    }

    private void ScheduleExpandAll()
    {
        // Nested TreeViewItems only exist after parents expand, so run a few passes.
        Dispatcher.UIThread.Post(() => ExpandAllTreeItems(pass: 0), DispatcherPriority.Loaded);
    }

    private void ExpandAllTreeItems(int pass)
    {
        var expandedAny = false;
        foreach (var item in WallsTree.GetVisualDescendants().OfType<TreeViewItem>())
        {
            if (item.IsExpanded)
            {
                continue;
            }

            item.IsExpanded = true;
            expandedAny = true;
        }

        if (expandedAny && pass < 8)
        {
            Dispatcher.UIThread.Post(() => ExpandAllTreeItems(pass + 1), DispatcherPriority.Loaded);
        }
    }
}
