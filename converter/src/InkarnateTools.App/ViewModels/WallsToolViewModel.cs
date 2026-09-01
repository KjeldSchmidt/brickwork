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

    [ObservableProperty]
    private int _treeRevision;

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

    partial void OnSelectedTreeItemChanged(object? value)
    {
        switch (value)
        {
            case WallItemViewModel wallItem:
                _session.SetFocusedWall(wallItem.Wall);
                break;
            case WallPortalItemViewModel portalItem:
                var wall = _session.Map?.Walls.FirstOrDefault(
                    candidate => candidate.Portals.Contains(portalItem.Portal));
                if (wall is not null)
                {
                    _session.SetFocusedWall(wall, portalItem.Portal);
                }

                break;
        }
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
        var layersById = _session.Map.Layers.ToDictionary(
            layer => layer.Id,
            StringComparer.OrdinalIgnoreCase);
        var wallsByLayer = _session.Map.Walls
            .GroupBy(wall => wall.LayerId ?? "(no layer)")
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var layerId in OrderLayerIds(wallsByLayer.Keys, _session.Map.Layers))
        {
            var layerWalls = wallsByLayer[layerId];
            var displayName = layersById.TryGetValue(layerId, out var mapLayer)
                ? mapLayer.DisplayName
                : layerId;
            var layerNode = new WallLayerNodeViewModel(_session, layerId, displayName);
            var layerWallIds = layerWalls.Select(wall => wall.EntityId).ToHashSet();

            var rootGroups = _session.Map.Groups
                .Where(group => IsRootGroup(group, groupsById))
                .Where(group => GroupHasDescendantWall(group, groupsById, wallsById, layerWallIds))
                .OrderBy(group => group.GroupId);

            foreach (var group in rootGroups)
            {
                var groupNode = BuildGroupNode(group, groupsById, wallsById, layerWallIds);
                if (groupNode is not null)
                {
                    layerNode.Children.Add(groupNode);
                }
            }

            foreach (var wall in layerWalls.Where(wall => wall.GroupId is null).OrderBy(wall => wall.EntityId))
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
        TreeRevision++;
        ApplyTreeFocusFromSession();
    }

    private static IEnumerable<string> OrderLayerIds(
        IEnumerable<string> wallLayerIds,
        IList<MapLayer> mapLayers)
    {
        var remaining = wallLayerIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in mapLayers.OrderBy(layer => layer.Order))
        {
            if (remaining.Remove(layer.Id))
            {
                yield return layer.Id;
            }
        }

        foreach (var orphanId in remaining.OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
        {
            yield return orphanId;
        }
    }

    private WallGroupNodeViewModel? BuildGroupNode(
        EntityGroup group,
        IReadOnlyDictionary<int, EntityGroup> groupsById,
        IReadOnlyDictionary<int, Wall> wallsById,
        HashSet<int> layerWallIds)
    {
        if (!GroupHasDescendantWall(group, groupsById, wallsById, layerWallIds))
        {
            return null;
        }

        var node = new WallGroupNodeViewModel(_session, group);

        foreach (var childGroupId in GetChildGroupIds(group, groupsById).OrderBy(id => id))
        {
            var childGroup = groupsById[childGroupId];
            var childNode = BuildGroupNode(childGroup, groupsById, wallsById, layerWallIds);
            if (childNode is not null)
            {
                node.Children.Add(childNode);
            }
        }

        foreach (var wall in GetChildWalls(group, wallsById, layerWallIds).OrderBy(wall => wall.EntityId))
        {
            node.Children.Add(new WallItemViewModel(_session, wall));
        }

        return node.Children.Count > 0 ? node : null;
    }

    private static bool IsRootGroup(EntityGroup group, IReadOnlyDictionary<int, EntityGroup> groupsById) =>
        group.ParentGroupId is not int parentId || !groupsById.ContainsKey(parentId);

    private static IEnumerable<int> GetChildGroupIds(
        EntityGroup group,
        IReadOnlyDictionary<int, EntityGroup> groupsById)
    {
        var seen = new HashSet<int>();
        foreach (var memberId in group.MemberIds)
        {
            if (groupsById.ContainsKey(memberId) && seen.Add(memberId))
            {
                yield return memberId;
            }
        }

        foreach (var childGroup in groupsById.Values)
        {
            if (childGroup.ParentGroupId == group.GroupId && seen.Add(childGroup.GroupId))
            {
                yield return childGroup.GroupId;
            }
        }
    }

    private static IEnumerable<Wall> GetChildWalls(
        EntityGroup group,
        IReadOnlyDictionary<int, Wall> wallsById,
        HashSet<int> layerWallIds)
    {
        var seen = new HashSet<int>();
        foreach (var memberId in group.MemberIds)
        {
            if (wallsById.TryGetValue(memberId, out var memberWall) &&
                layerWallIds.Contains(memberWall.EntityId) &&
                seen.Add(memberWall.EntityId))
            {
                yield return memberWall;
            }
        }

        foreach (var wall in wallsById.Values)
        {
            if (wall.GroupId == group.GroupId &&
                layerWallIds.Contains(wall.EntityId) &&
                seen.Add(wall.EntityId))
            {
                yield return wall;
            }
        }
    }

    private static bool GroupHasDescendantWall(
        EntityGroup group,
        IReadOnlyDictionary<int, EntityGroup> groupsById,
        IReadOnlyDictionary<int, Wall> wallsById,
        HashSet<int> layerWallIds)
    {
        if (GetChildWalls(group, wallsById, layerWallIds).Any())
        {
            return true;
        }

        foreach (var childGroupId in GetChildGroupIds(group, groupsById))
        {
            if (groupsById.TryGetValue(childGroupId, out var childGroup) &&
                GroupHasDescendantWall(childGroup, groupsById, wallsById, layerWallIds))
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

    public WallLayerNodeViewModel(EditorSession session, string layerId, string displayName)
    {
        _session = session;
        LayerId = layerId;
        DisplayName = displayName;
    }

    public string LayerId { get; }

    public string DisplayName { get; }

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
        OnPropertyChanged(nameof(DisplayName));
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
