using System.Collections.ObjectModel;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;
using Brickwork.Core.Models;

namespace Brickwork.App.ViewModels;

public partial class WallsToolViewModel : Tool
{
    private readonly EditorSession _session;
    private bool _syncingSelection;
    private int? _selectionAnchorWallId;

    [ObservableProperty]
    private ObservableCollection<WallLayerNodeViewModel> _layers = [];

    [ObservableProperty]
    private object? _selectedTreeItem;

    [ObservableProperty]
    private int _treeRevision;

    public bool HasLayers => Layers.Count > 0;

    public bool ShowEmptyMessage => !HasLayers;

    public string EmptyMessage => _session.Map is null
        ? "Open a Source Map to see walls"
        : "⚠️ It seems your map contains no paths. Note that for inkarnate maps, only paths are understood as walls - stamps that look like walls cannot be parsed.";

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
                SyncTreeWithWallSet();
            }

            if (args.PropertyName is nameof(EditorSession.TreeFocusGeneration))
            {
                ApplyTreeFocusFromSession();
            }

            if (args.PropertyName is nameof(EditorSession.HighlightRevision)
                or nameof(EditorSession.FocusedWallEntityId)
                or nameof(EditorSession.FocusedPortal)
                or nameof(EditorSession.HoveredWallEntityId)
                or nameof(EditorSession.HoveredPortal))
            {
                if (args.PropertyName is nameof(EditorSession.FocusedWallEntityId)
                    && _session.FocusedWallEntityId is null
                    && SelectedTreeItem is not null)
                {
                    _syncingSelection = true;
                    SelectedTreeItem = null;
                    _syncingSelection = false;
                    _selectionAnchorWallId = null;
                }

                RefreshHighlightStates();
            }
        };
        RebuildLayers();
    }

    public void SetHoveredTreeItem(object? item)
    {
        switch (item)
        {
            case WallItemViewModel wallItem:
                _session.SetHoveredWall(wallItem.Wall);
                break;
            case WallPortalItemViewModel portalItem:
                var wall = _session.Map?.Walls.FirstOrDefault(
                    candidate => candidate.Portals.Contains(portalItem.Portal));
                if (wall is not null)
                {
                    _session.SetHoveredWall(wall, portalItem.Portal);
                }

                break;
            default:
                _session.ClearHoveredWall();
                break;
        }
    }

    public void ClearTreeHover() => _session.ClearHoveredWall();

    public void ClearSelection()
    {
        if (SelectedTreeItem is not null)
        {
            _syncingSelection = true;
            SelectedTreeItem = null;
            _syncingSelection = false;
        }

        _selectionAnchorWallId = null;
        _session.ClearWallSelection();
    }

    public bool DeleteSelectedWalls() => _session.DeleteSelectedWalls();

    public void HandleTreeActivation(object? item, KeyModifiers modifiers)
    {
        switch (item)
        {
            case WallItemViewModel wallItem:
                HandleWallTreeClick(wallItem.Wall.EntityId, portal: null, modifiers);
                break;
            case WallPortalItemViewModel portalItem:
                HandleWallTreeClick(portalItem.WallEntityId, portalItem.Portal, modifiers);
                break;
            case WallGroupNodeViewModel group:
                HandleGroupTreeClick(EnumerateDescendantWallIds(group.Children), modifiers);
                break;
            case WallLayerNodeViewModel layer:
                HandleGroupTreeClick(EnumerateDescendantWallIds(layer.Children), modifiers);
                break;
            default:
                ClearSelection();
                break;
        }
    }

    private void HandleWallTreeClick(int wallId, WallPortal? portal, KeyModifiers modifiers)
    {
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            _session.ToggleWallInSelection(wallId);
            _selectionAnchorWallId = wallId;
            SyncSelectedTreeItemFromPrimary(portal);
            return;
        }

        if (modifiers.HasFlag(KeyModifiers.Shift) && _selectionAnchorWallId is int anchorId)
        {
            var ordered = EnumerateWallsInTreeOrder().ToList();
            var anchorIndex = ordered.FindIndex(id => id == anchorId);
            var clickIndex = ordered.FindIndex(id => id == wallId);
            if (anchorIndex >= 0 && clickIndex >= 0)
            {
                var start = Math.Min(anchorIndex, clickIndex);
                var end = Math.Max(anchorIndex, clickIndex);
                var range = ordered.Skip(start).Take(end - start + 1);
                _session.SetSelection(range, wallId, portal);
                SyncSelectedTreeItemFromPrimary(portal);
                return;
            }
        }

        _session.SetSelection([wallId], wallId, portal);
        _selectionAnchorWallId = wallId;
        SyncSelectedTreeItemFromPrimary(portal);
    }

    private void HandleGroupTreeClick(IReadOnlyList<int> wallIds, KeyModifiers modifiers)
    {
        if (wallIds.Count == 0)
        {
            return;
        }

        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            var anyMissing = wallIds.Any(id => !_session.SelectedWallEntityIds.Contains(id));
            if (anyMissing)
            {
                _session.AddWallsToSelection(wallIds);
            }
            else
            {
                _session.RemoveWallsFromSelection(wallIds);
            }

            _selectionAnchorWallId = wallIds[0];
            SyncSelectedTreeItemFromPrimary(portal: null);
            return;
        }

        _session.SetSelection(wallIds, wallIds[0]);
        _selectionAnchorWallId = wallIds[0];
        SyncSelectedTreeItemFromPrimary(portal: null);
    }

    private void SyncSelectedTreeItemFromPrimary(WallPortal? portal)
    {
        if (_session.FocusedWallEntityId is not int wallId)
        {
            _syncingSelection = true;
            SelectedTreeItem = null;
            _syncingSelection = false;
            return;
        }

        _syncingSelection = true;
        SelectedTreeItem = FindTreeItem(wallId, portal ?? _session.FocusedPortal);
        _syncingSelection = false;
    }

    private IReadOnlyList<int> EnumerateDescendantWallIds(IEnumerable<object> children) =>
        EnumerateTreeWallIds(children).ToList();

    private IEnumerable<int> EnumerateWallsInTreeOrder()
    {
        foreach (var layer in Layers)
        {
            foreach (var wallId in EnumerateTreeWallIds(layer.Children))
            {
                yield return wallId;
            }
        }
    }

    private void RefreshHighlightStates()
    {
        foreach (var layer in Layers)
        {
            layer.RefreshHighlightState();
            RefreshHighlightStates(layer.Children);
        }
    }

    private static void RefreshHighlightStates(IEnumerable<object> children)
    {
        foreach (var child in children)
        {
            switch (child)
            {
                case WallGroupNodeViewModel group:
                    group.RefreshHighlightState();
                    RefreshHighlightStates(group.Children);
                    break;
                case WallItemViewModel wall:
                    wall.RefreshHighlightState();
                    break;
            }
        }
    }

    partial void OnSelectedTreeItemChanged(object? value)
    {
        // Session selection is owned by HandleTreeActivation / ApplyTreeFocusFromSession.
        if (_syncingSelection)
        {
            return;
        }

        _ = value;
    }

    private void RefreshBoundValues()
    {
        foreach (var layer in Layers)
        {
            layer.RefreshFromModel();
        }
    }

    private void SyncTreeWithWallSet()
    {
        if (_session.Map is null)
        {
            if (Layers.Count > 0)
            {
                RebuildLayers();
            }

            return;
        }

        var mapWallIds = _session.Map.Walls.Select(wall => wall.EntityId).ToHashSet();
        var treeWallIds = EnumerateTreeWallIds().ToHashSet();
        if (mapWallIds.SetEquals(treeWallIds))
        {
            RefreshBoundValues();
            return;
        }

        // Removals only: drop matching nodes instead of rebuilding (avoids expand-all).
        if (mapWallIds.IsSubsetOf(treeWallIds))
        {
            foreach (var removedId in treeWallIds)
            {
                if (!mapWallIds.Contains(removedId))
                {
                    RemoveWallNode(removedId);
                }
            }

            OnPropertyChanged(nameof(HasLayers));
            OnPropertyChanged(nameof(ShowEmptyMessage));
            OnPropertyChanged(nameof(EmptyMessage));
            return;
        }

        // Additions only: insert matching nodes instead of rebuilding.
        if (treeWallIds.IsSubsetOf(mapWallIds))
        {
            foreach (var wall in _session.Map.Walls.OrderBy(candidate => candidate.EntityId))
            {
                if (!treeWallIds.Contains(wall.EntityId))
                {
                    InsertWallNode(wall);
                }
            }

            OnPropertyChanged(nameof(HasLayers));
            OnPropertyChanged(nameof(ShowEmptyMessage));
            OnPropertyChanged(nameof(EmptyMessage));
            return;
        }

        RebuildLayers();
    }

    private void InsertWallNode(Wall wall)
    {
        if (_session.Map is null)
        {
            return;
        }

        var layerNode = EnsureLayerNode(wall.LayerId ?? "(no layer)");
        var wallItem = new WallItemViewModel(_session, wall);

        if (wall.GroupId is not int groupId ||
            _session.Map.Groups.All(group => group.GroupId != groupId))
        {
            InsertWallItemSorted(layerNode.Children, wallItem);
            layerNode.RefreshActiveState();
            return;
        }

        var groupNode = EnsureGroupPath(layerNode, groupId);
        InsertWallItemSorted(groupNode.Children, wallItem);
        groupNode.RefreshActiveState();
        layerNode.RefreshActiveState();
    }

    private WallLayerNodeViewModel EnsureLayerNode(string layerId)
    {
        var existing = Layers.FirstOrDefault(
            layer => string.Equals(layer.LayerId, layerId, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var displayName = _session.Map?.Layers
            .FirstOrDefault(layer => string.Equals(layer.Id, layerId, StringComparison.OrdinalIgnoreCase))
            ?.DisplayName ?? layerId;
        var layerNode = new WallLayerNodeViewModel(_session, layerId, displayName);
        InsertLayerSorted(layerNode);
        return layerNode;
    }

    private void InsertLayerSorted(WallLayerNodeViewModel layerNode)
    {
        if (_session.Map is null)
        {
            Layers.Add(layerNode);
            return;
        }

        var orderedIds = OrderLayerIds(
                Layers.Select(layer => layer.LayerId).Append(layerNode.LayerId),
                _session.Map.Layers)
            .ToList();
        var insertAt = orderedIds.IndexOf(layerNode.LayerId);
        if (insertAt < 0 || insertAt >= Layers.Count)
        {
            Layers.Add(layerNode);
            return;
        }

        Layers.Insert(insertAt, layerNode);
    }

    private WallGroupNodeViewModel EnsureGroupPath(WallLayerNodeViewModel layerNode, int groupId)
    {
        var groupsById = _session.Map!.Groups.ToDictionary(group => group.GroupId);
        var chain = new List<EntityGroup>();
        var current = groupsById[groupId];
        while (true)
        {
            chain.Add(current);
            if (current.ParentGroupId is not int parentId ||
                !groupsById.TryGetValue(parentId, out current))
            {
                break;
            }
        }

        chain.Reverse();

        var children = layerNode.Children;
        WallGroupNodeViewModel? node = null;
        foreach (var group in chain)
        {
            node = children
                .OfType<WallGroupNodeViewModel>()
                .FirstOrDefault(candidate => candidate.Group.GroupId == group.GroupId);
            if (node is null)
            {
                node = new WallGroupNodeViewModel(_session, group);
                InsertGroupSorted(children, node);
            }

            children = node.Children;
        }

        return node!;
    }

    private static void InsertGroupSorted(ObservableCollection<object> children, WallGroupNodeViewModel groupNode)
    {
        var insertAt = 0;
        while (insertAt < children.Count &&
               children[insertAt] is WallGroupNodeViewModel existing &&
               existing.Group.GroupId < groupNode.Group.GroupId)
        {
            insertAt++;
        }

        children.Insert(insertAt, groupNode);
    }

    private static void InsertWallItemSorted(ObservableCollection<object> children, WallItemViewModel wallItem)
    {
        var insertAt = 0;
        while (insertAt < children.Count && children[insertAt] is WallGroupNodeViewModel)
        {
            insertAt++;
        }

        while (insertAt < children.Count &&
               children[insertAt] is WallItemViewModel existing &&
               existing.Wall.EntityId < wallItem.Wall.EntityId)
        {
            insertAt++;
        }

        children.Insert(insertAt, wallItem);
    }

    private void RemoveWallNode(int wallEntityId)
    {
        if (SelectedTreeItem is WallItemViewModel selectedWall &&
            selectedWall.Wall.EntityId == wallEntityId)
        {
            _syncingSelection = true;
            SelectedTreeItem = null;
            _syncingSelection = false;
        }
        else if (SelectedTreeItem is WallPortalItemViewModel selectedPortal &&
                 selectedPortal.WallEntityId == wallEntityId)
        {
            _syncingSelection = true;
            SelectedTreeItem = null;
            _syncingSelection = false;
        }

        for (var layerIndex = Layers.Count - 1; layerIndex >= 0; layerIndex--)
        {
            var layer = Layers[layerIndex];
            if (!TryRemoveWallFromChildren(layer.Children, wallEntityId))
            {
                continue;
            }

            if (layer.Children.Count == 0)
            {
                Layers.RemoveAt(layerIndex);
            }
            else
            {
                layer.RefreshActiveState();
            }

            return;
        }
    }

    private static bool TryRemoveWallFromChildren(ObservableCollection<object> children, int wallEntityId)
    {
        for (var index = 0; index < children.Count; index++)
        {
            switch (children[index])
            {
                case WallItemViewModel wall when wall.Wall.EntityId == wallEntityId:
                    children.RemoveAt(index);
                    return true;
                case WallGroupNodeViewModel group:
                    if (!TryRemoveWallFromChildren(group.Children, wallEntityId))
                    {
                        break;
                    }

                    if (group.Children.Count == 0)
                    {
                        children.RemoveAt(index);
                    }
                    else
                    {
                        group.RefreshActiveState();
                    }

                    return true;
            }
        }

        return false;
    }

    private IEnumerable<int> EnumerateTreeWallIds()
    {
        foreach (var layer in Layers)
        {
            foreach (var wallId in EnumerateTreeWallIds(layer.Children))
            {
                yield return wallId;
            }
        }
    }

    private static IEnumerable<int> EnumerateTreeWallIds(IEnumerable<object> children)
    {
        foreach (var child in children)
        {
            switch (child)
            {
                case WallGroupNodeViewModel group:
                    foreach (var wallId in EnumerateTreeWallIds(group.Children))
                    {
                        yield return wallId;
                    }

                    break;
                case WallItemViewModel wall:
                    yield return wall.Wall.EntityId;
                    break;
            }
        }
    }

    private void ApplyTreeFocusFromSession()
    {
        if (_session.FocusedWallEntityId is not int wallId)
        {
            return;
        }

        _selectionAnchorWallId = wallId;
        _syncingSelection = true;
        SelectedTreeItem = FindTreeItem(wallId, _session.FocusedPortal);
        _syncingSelection = false;
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
        _syncingSelection = true;
        SelectedTreeItem = null;
        _syncingSelection = false;
        Layers.Clear();
        OnPropertyChanged(nameof(HasLayers));
        OnPropertyChanged(nameof(ShowEmptyMessage));
        OnPropertyChanged(nameof(EmptyMessage));

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
        OnPropertyChanged(nameof(EmptyMessage));
        TreeRevision++;
        ApplyTreeFocusFromSession();
        RefreshHighlightStates();
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

    public bool IsTreeHighlighted
    {
        get
        {
            var wallIds = EnumerateDescendantWallIds(Children).ToList();
            return wallIds.Count > 0 && wallIds.Any(id => _session.SelectedWallEntityIds.Contains(id));
        }
    }

    public bool? IsActive
    {
        get => WallTreeActiveState.Compute(Children);
        set
        {
            var enabled = ResolveCascadeTarget(value, IsActive);
            _session.Execute("Set layer active", () =>
            {
                WallTreeActiveState.Apply(Children, enabled);
            });
            OnPropertyChanged();
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

    public void RefreshActiveState() => OnPropertyChanged(nameof(IsActive));

    public void RefreshHighlightState() => OnPropertyChanged(nameof(IsTreeHighlighted));

    private static bool ResolveCascadeTarget(bool? requested, bool? current) =>
        requested ?? current != true;

    private static IEnumerable<int> EnumerateDescendantWallIds(IEnumerable<object> children)
    {
        foreach (var child in children)
        {
            switch (child)
            {
                case WallGroupNodeViewModel group:
                    foreach (var id in EnumerateDescendantWallIds(group.Children))
                    {
                        yield return id;
                    }

                    break;
                case WallItemViewModel wall:
                    yield return wall.Wall.EntityId;
                    break;
            }
        }
    }
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

    public bool IsTreeHighlighted
    {
        get
        {
            var wallIds = EnumerateDescendantWallIds(Children).ToList();
            return wallIds.Count > 0 && wallIds.Any(id => _session.SelectedWallEntityIds.Contains(id));
        }
    }

    public bool? IsActive
    {
        get => WallTreeActiveState.Compute(Children);
        set
        {
            var enabled = ResolveCascadeTarget(value, IsActive);
            _session.Execute("Set group active", () =>
            {
                WallTreeActiveState.Apply(Children, enabled);
            });
            OnPropertyChanged();
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

    public void RefreshActiveState() => OnPropertyChanged(nameof(IsActive));

    public void RefreshHighlightState() => OnPropertyChanged(nameof(IsTreeHighlighted));

    private static bool ResolveCascadeTarget(bool? requested, bool? current) =>
        requested ?? current != true;

    private static IEnumerable<int> EnumerateDescendantWallIds(IEnumerable<object> children)
    {
        foreach (var child in children)
        {
            switch (child)
            {
                case WallGroupNodeViewModel group:
                    foreach (var id in EnumerateDescendantWallIds(group.Children))
                    {
                        yield return id;
                    }

                    break;
                case WallItemViewModel wall:
                    yield return wall.Wall.EntityId;
                    break;
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

        var portalNumber = 1;
        foreach (var portal in wall.Portals)
        {
            Portals.Add(new WallPortalItemViewModel(session, wall.EntityId, portal, portalNumber));
            portalNumber++;
        }
    }

    public Wall Wall { get; }

    public ObservableCollection<WallPortalItemViewModel> Portals { get; } = [];

    public IReadOnlyList<WallLineType> LineTypeOptions { get; } =
        Enum.GetValues<WallLineType>();

    public string DisplayName => Wall.DisplayName;

    public bool IsFocused =>
        _session.FocusedWallEntityId == Wall.EntityId && _session.FocusedPortal is null;

    public bool IsSelected => _session.SelectedWallEntityIds.Contains(Wall.EntityId);

    public bool IsHovered =>
        _session.HoveredWallEntityId == Wall.EntityId && _session.HoveredPortal is null;

    public bool IsTreeHighlighted => IsSelected || IsHovered;

    public bool IsActive
    {
        get => Wall.IsActive && Wall.LineType != WallLineType.Disabled;
        set
        {
            var enabled = Wall.IsActive && Wall.LineType != WallLineType.Disabled;
            if (enabled == value)
            {
                return;
            }

            _session.Execute(value ? "Enable wall" : "Disable wall", () =>
            {
                WallLineEditing.SetWallEnabled(Wall, value);
            });
            OnPropertyChanged();
            OnPropertyChanged(nameof(LineType));
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

            _session.Execute("Change wall type", () =>
            {
                Wall.LineType = value;
                Wall.IsActive = value != WallLineType.Disabled;
            });
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsActive));
        }
    }

    public void RefreshFromModel()
    {
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(LineType));
        OnPropertyChanged(nameof(DisplayName));
        SyncPortalsFromWall();
        RefreshHighlightState();
    }

    private void SyncPortalsFromWall()
    {
        for (var index = Portals.Count - 1; index >= 0; index--)
        {
            if (!Wall.Portals.Contains(Portals[index].Portal))
            {
                Portals.RemoveAt(index);
            }
        }

        var nextNumber = Portals.Count == 0
            ? 1
            : Portals.Max(item => item.PortalNumber) + 1;

        foreach (var portal in Wall.Portals)
        {
            var existing = Portals.FirstOrDefault(item => ReferenceEquals(item.Portal, portal));
            if (existing is null)
            {
                Portals.Add(new WallPortalItemViewModel(_session, Wall.EntityId, portal, nextNumber));
                nextNumber++;
            }
            else
            {
                existing.RefreshFromModel();
            }
        }
    }

    public void RefreshHighlightState()
    {
        OnPropertyChanged(nameof(IsFocused));
        OnPropertyChanged(nameof(IsSelected));
        OnPropertyChanged(nameof(IsHovered));
        OnPropertyChanged(nameof(IsTreeHighlighted));
        foreach (var portal in Portals)
        {
            portal.RefreshHighlightState();
        }
    }
}

public partial class WallPortalItemViewModel : ObservableObject
{
    private readonly EditorSession _session;

    public WallPortalItemViewModel(EditorSession session, int wallEntityId, WallPortal portal, int portalNumber)
    {
        _session = session;
        WallEntityId = wallEntityId;
        Portal = portal;
        PortalNumber = portalNumber;
    }

    public int WallEntityId { get; }

    public WallPortal Portal { get; }

    public int PortalNumber { get; }

    public IReadOnlyList<WallLineType> LineTypeOptions { get; } =
        Enum.GetValues<WallLineType>();

    public string DisplayName => $"Portal {PortalNumber}";

    public bool IsFocused =>
        _session.FocusedWallEntityId == WallEntityId &&
        ReferenceEquals(_session.FocusedPortal, Portal);

    public bool IsHovered =>
        _session.HoveredWallEntityId == WallEntityId &&
        ReferenceEquals(_session.HoveredPortal, Portal);

    public bool IsTreeHighlighted => IsFocused || IsHovered;

    public bool IsActive
    {
        get => Portal.IsActive;
        set
        {
            if (Portal.IsActive == value)
            {
                return;
            }

            _session.Execute("Set portal active", () =>
            {
                Portal.IsActive = value;
            });
            OnPropertyChanged();
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

            _session.Execute("Change portal type", () =>
            {
                Portal.LineType = value;
            });
            OnPropertyChanged();
        }
    }

    public void RefreshFromModel()
    {
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(LineType));
        RefreshHighlightState();
    }

    public void RefreshHighlightState()
    {
        OnPropertyChanged(nameof(IsFocused));
        OnPropertyChanged(nameof(IsHovered));
        OnPropertyChanged(nameof(IsTreeHighlighted));
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
                    WallLineEditing.SetWallEnabled(wall.Wall, enabled);
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
                    yield return wall.Wall.IsActive && wall.Wall.LineType != WallLineType.Disabled;
                    foreach (var portal in wall.Wall.Portals)
                    {
                        yield return portal.IsActive;
                    }

                    break;
            }
        }
    }
}
