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
            RefreshChildren(layer.Children);
        }
    }

    private static void RefreshChildren(IEnumerable<object> children)
    {
        foreach (var child in children)
        {
            switch (child)
            {
                case WallGroupNodeViewModel group:
                    group.RefreshFromModel();
                    RefreshChildren(group.Children);
                    break;
                case WallItemViewModel wall:
                    wall.RefreshFromModel();
                    break;
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

        var groupsById = _session.Map.Groups.ToDictionary(group => group.GroupId);
        var wallsById = _session.Map.Walls.ToDictionary(wall => wall.EntityId);
        var wallsByLayer = _session.Map.Walls
            .GroupBy(wall => wall.LayerId ?? "(no layer)")
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var layerGroup in wallsByLayer)
        {
            var layerNode = new WallLayerNodeViewModel(layerGroup.Key);
            var layerWallIds = layerGroup.Select(wall => wall.EntityId).ToHashSet();

            var rootGroups = _session.Map.Groups
                .Where(group => group.ParentGroupId is null)
                .Where(group => GroupHasDescendantWall(group, groupsById, layerWallIds))
                .OrderBy(group => group.GroupId);

            foreach (var group in rootGroups)
            {
                var groupNode = BuildGroupNode(group, groupsById, wallsById, layerWallIds);
                if (groupNode is not null)
                {
                    layerNode.Children.Add(groupNode);
                }
            }

            foreach (var wall in layerGroup.Where(wall => wall.GroupId is null).OrderBy(wall => wall.EntityId))
            {
                layerNode.Children.Add(new WallItemViewModel(_session, wall));
            }

            if (layerNode.Children.Count > 0)
            {
                Layers.Add(layerNode);
            }
        }

        OnPropertyChanged(nameof(HasLayers));
        OnPropertyChanged(nameof(ShowEmptyMessage));
    }

    private WallGroupNodeViewModel? BuildGroupNode(
        EntityGroup group,
        IReadOnlyDictionary<int, EntityGroup> groupsById,
        IReadOnlyDictionary<int, Wall> wallsById,
        HashSet<int> layerWallIds)
    {
        if (!GroupHasDescendantWall(group, groupsById, layerWallIds))
        {
            return null;
        }

        var node = new WallGroupNodeViewModel(group);
        foreach (var memberId in group.MemberIds)
        {
            if (groupsById.TryGetValue(memberId, out var childGroup))
            {
                var childNode = BuildGroupNode(childGroup, groupsById, wallsById, layerWallIds);
                if (childNode is not null)
                {
                    node.Children.Add(childNode);
                }
            }
            else if (wallsById.TryGetValue(memberId, out var wall) && layerWallIds.Contains(wall.EntityId))
            {
                node.Children.Add(new WallItemViewModel(_session, wall));
            }
        }

        return node.Children.Count > 0 ? node : null;
    }

    private static bool GroupHasDescendantWall(
        EntityGroup group,
        IReadOnlyDictionary<int, EntityGroup> groupsById,
        HashSet<int> layerWallIds)
    {
        foreach (var memberId in group.MemberIds)
        {
            if (layerWallIds.Contains(memberId))
            {
                return true;
            }

            if (groupsById.TryGetValue(memberId, out var childGroup) &&
                GroupHasDescendantWall(childGroup, groupsById, layerWallIds))
            {
                return true;
            }
        }

        return false;
    }
}

public partial class WallLayerNodeViewModel : ObservableObject
{
    public WallLayerNodeViewModel(string layerId)
    {
        LayerId = layerId;
    }

    public string LayerId { get; }

    public ObservableCollection<object> Children { get; } = [];
}

public partial class WallGroupNodeViewModel : ObservableObject
{
    public WallGroupNodeViewModel(EntityGroup group)
    {
        Group = group;
    }

    public EntityGroup Group { get; }

    public ObservableCollection<object> Children { get; } = [];

    public string DisplayName => Group.DisplayName;

    public void RefreshFromModel()
    {
        OnPropertyChanged(nameof(DisplayName));
        foreach (var child in Children)
        {
            if (child is WallItemViewModel wall)
            {
                wall.RefreshFromModel();
            }
            else if (child is WallGroupNodeViewModel group)
            {
                group.RefreshFromModel();
            }
        }
    }
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
        OnPropertyChanged(nameof(DisplayName));

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
