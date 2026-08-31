using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;
using InkarnateTools.Core.Models;

namespace InkarnateTools.App.ViewModels;

public partial class WallsToolViewModel : Tool
{
    private readonly EditorSession _session;

    [ObservableProperty]
    private ObservableCollection<WallLayerNodeViewModel> _layers = [];

    public bool HasLayers => Layers.Count > 0;

    public bool ShowEmptyMessage => !HasLayers;

    public WallsToolViewModel(EditorSession session)
    {
        _session = session;
        _session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(EditorSession.Map))
            {
                RebuildLayers();
            }
        };
        RebuildLayers();
    }

    private void RebuildLayers()
    {
        Layers.Clear();
        OnPropertyChanged(nameof(HasLayers));
        OnPropertyChanged(nameof(ShowEmptyMessage));

        if (_session.Map is null)
        {
            return;
        }

        foreach (var layerGroup in _session.Map.Walls
                     .GroupBy(wall => wall.LayerId ?? "(no layer)")
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var layerNode = new WallLayerNodeViewModel(layerGroup.Key);
            foreach (var wall in layerGroup.OrderBy(w => w.EntityId))
            {
                layerNode.Walls.Add(new WallItemViewModel(_session, wall));
            }

            Layers.Add(layerNode);
        }

        OnPropertyChanged(nameof(HasLayers));
        OnPropertyChanged(nameof(ShowEmptyMessage));
    }
}

public partial class WallLayerNodeViewModel : ObservableObject
{
    public WallLayerNodeViewModel(string layerId)
    {
        LayerId = layerId;
    }

    public string LayerId { get; }

    public ObservableCollection<WallItemViewModel> Walls { get; } = [];
}

public partial class WallItemViewModel : ObservableObject
{
    private readonly EditorSession _session;

    public WallItemViewModel(EditorSession session, Wall wall)
    {
        _session = session;
        Wall = wall;
    }

    public Wall Wall { get; }

    public string DisplayName => Wall.DisplayName;

    public bool IsActive
    {
        get => Wall.IsActive;
        set
        {
            if (Wall.IsActive == value)
            {
                return;
            }

            Wall.IsActive = value;
            OnPropertyChanged();
            _session.NotifyContentChanged();
        }
    }
}
