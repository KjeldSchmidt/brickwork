using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using InkarnateTools.App.ViewModels;
using InkarnateTools.Core.Models;

namespace InkarnateTools.App.Views.Map;

public partial class MapPreviewDocumentView : UserControl
{
    private MapDocument? _lastFittedMap;

    public MapPreviewDocumentView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        MapZoom.LayoutUpdated += OnMapZoomLayoutUpdated;
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
