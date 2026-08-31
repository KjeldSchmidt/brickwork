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

    [ObservableProperty]
    private object? _selectedTreeItem;

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

            if (args.PropertyName is nameof(EditorSession.TreeFocusGeneration))
            {
                ApplyTreeFocusFromSession();
            }
        };
        RebuildLayers();
    }

    private void RefreshBoundValues()
    {
        foreach (var layer in Layers)
        {
            layer.RefreshFromModel();
        }
    }

    private void ApplyTreeFocusFromSession()
    {
        if (_session.FocusedWallEntityId is not int wallId)
        {
            return;
        }

        SelectedTreeItem = FindTreeItem(wallId, _session.FocusedPortal);
    }

    private object? FindTreeItem(int wallEntityId, WallPortal? portal)
    {
        foreach (var layer in Layers)
        {
            var match = FindTreeItem(layer.Children, wallEntityId, portal);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static object? FindTreeItem(
        IEnumerable<object> children,
        int wallEntityId,
        WallPortal? portal)
    {
        foreach (var child in children)
        {
            switch (child)
            {
                case WallGroupNodeViewModel group:
                {
                    var nested = FindTreeItem(group.Children, wallEntityId, portal);
                    if (nested is not null)
                    {
                        return nested;
                    }

                    break;
                }
                case WallItemViewModel wall when wall.Wall.EntityId == wallEntityId:
                {
                    if (portal is null)
                    {
                        return wall;
                    }

                    foreach (var portalItem in wall.Portals)
                    {
                        if (ReferenceEquals(portalItem.Portal, portal))
                        {
                            return portalItem;
                        }
                    }

                    return wall;
                }
            }
        }

        return null;
    }

    private void RebuildLayers()
    {
        SelectedTreeItem = null;
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
            var layerNode = new WallLayerNodeViewModel(_session, layerGroup.Key);
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
        ApplyTreeFocusFromSession();
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

        var node = new WallGroupNodeViewModel(_session, group);
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
    private readonly EditorSession _session;

    public WallLayerNodeViewModel(EditorSession session, string layerId)
    {
        _session = session;
        LayerId = layerId;
    }

    public string LayerId { get; }

    public ObservableCollection<object> Children { get; } = [];

    public bool? IsActive
    {
        get => WallTreeActiveState.Compute(Children);
        set
        {
            var enabled = ResolveCascadeTarget(value, IsActive);
            WallTreeActiveState.Apply(Children, enabled);
            OnPropertyChanged();
            _session.NotifyContentChanged();
        }
    }

    public void RefreshFromModel()
    {
        OnPropertyChanged(nameof(IsActive));
        foreach (var child in Children)
        {
            switch (child)
            {
                case WallGroupNodeViewModel group:
                    group.RefreshFromModel();
                    break;
                case WallItemViewModel wall:
                    wall.RefreshFromModel();
                    break;
            }
        }
    }

    private static bool ResolveCascadeTarget(bool? requested, bool? current) =>
        requested ?? current != true;
}

public partial class WallGroupNodeViewModel : ObservableObject
{
    private readonly EditorSession _session;

    public WallGroupNodeViewModel(EditorSession session, EntityGroup group)
    {
        _session = session;
        Group = group;
    }

    public EntityGroup Group { get; }

    public ObservableCollection<object> Children { get; } = [];

    public string DisplayName => Group.DisplayName;

    public bool? IsActive
    {
        get => WallTreeActiveState.Compute(Children);
        set
        {
            var enabled = ResolveCascadeTarget(value, IsActive);
            WallTreeActiveState.Apply(Children, enabled);
            OnPropertyChanged();
            _session.NotifyContentChanged();
        }
    }

    public void RefreshFromModel()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(IsActive));
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

    private static bool ResolveCascadeTarget(bool? requested, bool? current) =>
        requested ?? current != true;
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

internal static class WallTreeActiveState
{
    public static bool? Compute(IEnumerable<object> children)
    {
        bool? state = null;
        var any = false;

        foreach (var flag in EnumerateActiveFlags(children))
        {
            any = true;
            if (state is null)
            {
                state = flag;
            }
            else if (state != flag)
            {
                return null;
            }
        }

        return any ? state : true;
    }

    public static void Apply(IEnumerable<object> children, bool enabled)
    {
        foreach (var child in children)
        {
            switch (child)
            {
                case WallGroupNodeViewModel group:
                    Apply(group.Children, enabled);
                    break;
                case WallItemViewModel wall:
                    wall.Wall.IsActive = enabled;
                    foreach (var portal in wall.Wall.Portals)
                    {
                        portal.IsActive = enabled;
                    }

                    break;
            }
        }
    }

    private static IEnumerable<bool> EnumerateActiveFlags(IEnumerable<object> children)
    {
        foreach (var child in children)
        {
            switch (child)
            {
                case WallGroupNodeViewModel group:
                    foreach (var flag in EnumerateActiveFlags(group.Children))
                    {
                        yield return flag;
                    }

                    break;
                case WallItemViewModel wall:
                    yield return wall.Wall.IsActive;
                    foreach (var portal in wall.Wall.Portals)
                    {
                        yield return portal.IsActive;
                    }

                    break;
            }
        }
    }
}
