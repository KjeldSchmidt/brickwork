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
        Dispatcher.UIThread.Post(ExpandAllTreeItems, DispatcherPriority.Loaded);
    }

    private void ExpandAllTreeItems()
    {
        foreach (var item in WallsTree.GetVisualDescendants().OfType<TreeViewItem>())
        {
            item.IsExpanded = true;
        }
    }
}
