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

            if (args.PropertyName is nameof(EditorSession.ContentRevision))
            {
                RefreshBoundValues();
            }
        };
        RebuildLayers();
    }

    private void RefreshBoundValues()
    {
        foreach (var layer in Layers)
        {
            foreach (var wall in layer.Walls)
            {
                wall.RefreshFromModel();
            }
        }
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

        foreach (var portal in wall.Portals)
        {
            Portals.Add(new WallPortalItemViewModel(session, portal));
        }
    }

    public Wall Wall { get; }

    public ObservableCollection<WallPortalItemViewModel> Portals { get; } = [];

    public IReadOnlyList<WallLineType> LineTypeOptions { get; } =
        Enum.GetValues<WallLineType>();

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

    public WallLineType LineType
    {
        get => Wall.LineType;
        set
        {
            if (Wall.LineType == value)
            {
                return;
            }

            Wall.LineType = value;
            OnPropertyChanged();
            _session.NotifyContentChanged();
        }
    }

    public void RefreshFromModel()
    {
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(LineType));

        foreach (var portal in Portals)
        {
            portal.RefreshFromModel();
        }
    }
}

public partial class WallPortalItemViewModel : ObservableObject
{
    private readonly EditorSession _session;

    public WallPortalItemViewModel(EditorSession session, WallPortal portal)
    {
        _session = session;
        Portal = portal;
    }

    public WallPortal Portal { get; }

    public IReadOnlyList<WallLineType> LineTypeOptions { get; } =
        Enum.GetValues<WallLineType>();

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Portal.Id) ? "Gap" : Portal.Id;

    public bool IsActive
    {
        get => Portal.IsActive;
        set
        {
            if (Portal.IsActive == value)
            {
                return;
            }

            Portal.IsActive = value;
            OnPropertyChanged();
            _session.NotifyContentChanged();
        }
    }

    public WallLineType LineType
    {
        get => Portal.LineType;
        set
        {
            if (Portal.LineType == value)
            {
                return;
            }

            Portal.LineType = value;
            OnPropertyChanged();
            _session.NotifyContentChanged();
        }
    }

    public void RefreshFromModel()
    {
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(LineType));
    }
}
