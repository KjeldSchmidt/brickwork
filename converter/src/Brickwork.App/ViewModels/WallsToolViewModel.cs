using System.Collections.ObjectModel;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;
using Brickwork.Core.Geometry;
using Brickwork.Core.Models;

namespace Brickwork.App.ViewModels;

public partial class WallsToolViewModel : Tool
{
    private readonly EditorSession _session;
    private readonly HashSet<object> _selectedTreeNodes = new(ReferenceEqualityComparer.Instance);
    private bool _syncingSelection;
    private int? _selectionAnchorWallId;
    private object? _treeSelectionAnchor;
    private IReadOnlyList<object>? _renameTargets;

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

        _selectedTreeNodes.Clear();
        _treeSelectionAnchor = null;
        _selectionAnchorWallId = null;
        _session.ClearWallSelection();
    }

    public bool IsTreeNodeSelected(object? item) =>
        item switch
        {
            WallItemViewModel wall =>
                _session.SelectedWallEntityIds.Contains(wall.Wall.EntityId),
            WallGroupNodeViewModel group =>
                _selectedTreeNodes.Contains(group) || AreAllDescendantWallsSelected(group.Children),
            WallLayerNodeViewModel layer =>
                _selectedTreeNodes.Contains(layer) || AreAllDescendantWallsSelected(layer.Children),
            WallPortalItemViewModel portal =>
                _selectedTreeNodes.Contains(portal),
            _ => item is not null && _selectedTreeNodes.Contains(item),
        };

    public void FocusTreeItem(object? item)
    {
        if (item is null)
        {
            return;
        }

        _syncingSelection = true;
        SelectedTreeItem = item;
        _syncingSelection = false;
    }

    public bool DeleteSelectedWalls() => _session.DeleteSelectedWalls();

    public bool CanRename(object? item) =>
        item is WallLayerNodeViewModel
            or WallGroupNodeViewModel
            or WallItemViewModel
            or WallPortalItemViewModel;

    public bool CanDelete(object? item) =>
        item is WallLayerNodeViewModel
            or WallGroupNodeViewModel
            or WallItemViewModel
            or WallPortalItemViewModel;

    public IReadOnlyList<object> GetEditTargets(object? item)
    {
        if (item is null || (!CanRename(item) && !CanDelete(item)))
        {
            return [];
        }

        return ResolveEditTargets(item);
    }

    public bool DeleteTreeItem(object? item)
    {
        if (_session.Map is null || item is null)
        {
            return false;
        }

        var targets = ResolveEditTargets(item);
        if (targets.Count == 0)
        {
            return false;
        }

        var deleted = false;
        var actionName = targets.Count == 1 ? "Delete" : "Delete selection";
        _session.Execute(actionName, () =>
        {
            foreach (var target in targets)
            {
                deleted |= DeleteTarget(target);
            }
        });

        if (deleted)
        {
            ClearSelection();
        }

        return deleted;
    }

    private bool DeleteTarget(object target)
    {
        if (_session.Map is null)
        {
            return false;
        }

        switch (target)
        {
            case WallItemViewModel wallItem:
                return WallLineEditing.RemoveFromMap(_session.Map, wallItem.Wall);

            case WallPortalItemViewModel portalItem:
            {
                var wall = _session.Map.Walls.FirstOrDefault(candidate =>
                    candidate.EntityId == portalItem.WallEntityId);
                return wall is not null && WallGeometryEditing.TryRemovePortal(wall, portalItem.Portal);
            }

            case WallGroupNodeViewModel groupItem:
            {
                var walls = EnumerateDescendantWallIds(groupItem.Children)
                    .Select(id => _session.Map.Walls.FirstOrDefault(candidate => candidate.EntityId == id))
                    .Where(wall => wall is not null)
                    .Cast<Wall>()
                    .ToList();
                var removed = false;
                foreach (var wall in walls)
                {
                    removed |= WallLineEditing.RemoveFromMap(_session.Map, wall);
                }

                return removed;
            }

            case WallLayerNodeViewModel layerItem:
            {
                var layerId = layerItem.LayerId;
                var walls = _session.Map.Walls
                    .Where(wall =>
                        string.Equals(
                            wall.LayerId ?? "(no layer)",
                            layerId,
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var removed = false;
                foreach (var wall in walls)
                {
                    removed |= WallLineEditing.RemoveFromMap(_session.Map, wall);
                }

                return removed;
            }

            default:
                return false;
        }
    }

    public bool BeginRenameSelection()
    {
        var primary = SelectedTreeItem;
        if (!CanRename(primary))
        {
            primary = _selectedTreeNodes.FirstOrDefault(CanRename);
        }

        if (primary is null)
        {
            return false;
        }

        BeginRename(primary);
        return true;
    }

    public void BeginRename(object? item)
    {
        CancelRename();
        if (!CanRename(item))
        {
            return;
        }

        _renameTargets = ResolveEditTargets(item!);
        BeginRenameEdit(item!);
    }

    public void CommitRename(object? item)
    {
        if (item is null)
        {
            return;
        }

        var renameText = TakeRenameText(item);
        var targets = _renameTargets ?? ResolveEditTargets(item);
        _renameTargets = null;
        CancelRename();

        if (renameText is null)
        {
            return;
        }

        var name = string.IsNullOrWhiteSpace(renameText) ? null : renameText.Trim();
        ApplyRename(targets, name);
    }

    public void CancelRename()
    {
        _renameTargets = null;
        foreach (var layer in Layers)
        {
            CancelRenameInTree(layer);
        }
    }

    /// <summary>
    /// Resolves mass-edit targets of the same kind as <paramref name="item"/> without
    /// descending into children (e.g. renaming groups does not rename nested walls).
    /// Walls use session multi-selection; groups/layers use fully-selected containers
    /// and/or explicit tree multi-selection.
    /// </summary>
    private IReadOnlyList<object> ResolveEditTargets(object item)
    {
        switch (item)
        {
            case WallItemViewModel:
            {
                var walls = EnumerateAllWallItems()
                    .Where(wall => _session.SelectedWallEntityIds.Contains(wall.Wall.EntityId))
                    .Cast<object>()
                    .ToList();
                return walls.Count > 0 ? walls : [item];
            }

            case WallPortalItemViewModel:
            {
                var portals = CollectSameTypeTreeTargets(item);
                return portals.Count > 0 ? portals : [item];
            }

            case WallGroupNodeViewModel:
            {
                var groups = EnumerateAllGroupItems()
                    .Where(group =>
                        ReferenceEquals(group, item) ||
                        _selectedTreeNodes.Contains(group) ||
                        AreAllDescendantWallsSelected(group.Children))
                    .Cast<object>()
                    .ToList();
                return groups.Count > 0 ? groups : [item];
            }

            case WallLayerNodeViewModel:
            {
                var layers = Layers
                    .Where(layer =>
                        ReferenceEquals(layer, item) ||
                        _selectedTreeNodes.Contains(layer) ||
                        AreAllDescendantWallsSelected(layer.Children))
                    .Cast<object>()
                    .ToList();
                return layers.Count > 0 ? layers : [item];
            }

            default:
                return [item];
        }
    }

    private List<object> CollectSameTypeTreeTargets(object item)
    {
        var sameType = _selectedTreeNodes
            .Where(candidate => candidate.GetType() == item.GetType())
            .ToList();
        if (sameType.Count > 0 && sameType.Contains(item))
        {
            return sameType;
        }

        return [item];
    }

    private bool AreAllDescendantWallsSelected(IEnumerable<object> children)
    {
        var wallIds = EnumerateDescendantWallIds(children);
        return wallIds.Count > 0 &&
               wallIds.All(id => _session.SelectedWallEntityIds.Contains(id));
    }

    private IEnumerable<WallItemViewModel> EnumerateAllWallItems()
    {
        foreach (var node in EnumerateTreeNodesInOrder())
        {
            if (node is WallItemViewModel wall)
            {
                yield return wall;
            }
        }
    }

    private IEnumerable<WallGroupNodeViewModel> EnumerateAllGroupItems()
    {
        foreach (var node in EnumerateTreeNodesInOrder())
        {
            if (node is WallGroupNodeViewModel group)
            {
                yield return group;
            }
        }
    }

    private void ApplyRename(IReadOnlyList<object> targets, string? name)
    {
        if (targets.Count == 0 || _session.Map is null)
        {
            return;
        }

        var actionName = targets.Count == 1 ? "Rename" : "Rename selection";
        _session.Execute(actionName, () =>
        {
            foreach (var target in targets)
            {
                switch (target)
                {
                    case WallLayerNodeViewModel layer:
                        ApplyLayerName(layer, name);
                        break;
                    case WallGroupNodeViewModel group:
                        group.Group.Name = name;
                        break;
                    case WallItemViewModel wall:
                        wall.Wall.Name = name;
                        break;
                    case WallPortalItemViewModel portal:
                        portal.Portal.Name = name;
                        break;
                }
            }
        });

        foreach (var target in targets)
        {
            NotifyDisplayName(target);
        }
    }

    private void ApplyLayerName(WallLayerNodeViewModel layerNode, string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || _session.Map is null)
        {
            return;
        }

        var layerId = layerNode.LayerId;
        var layer = _session.Map.Layers.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, layerId, StringComparison.OrdinalIgnoreCase));
        if (layer is null)
        {
            if (string.Equals(layerId, "(no layer)", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            layer = new MapLayer
            {
                Id = layerId,
                Name = name,
                IsVisible = true,
                Order = _session.Map.Layers.Count == 0
                    ? 0
                    : _session.Map.Layers.Max(candidate => candidate.Order) + 1,
            };
            _session.Map.Layers.Add(layer);
        }

        layer.Name = name;
    }

    private static void BeginRenameEdit(object item)
    {
        switch (item)
        {
            case WallLayerNodeViewModel layer:
                layer.BeginRename();
                break;
            case WallGroupNodeViewModel group:
                group.BeginRename();
                break;
            case WallItemViewModel wall:
                wall.BeginRename();
                break;
            case WallPortalItemViewModel portal:
                portal.BeginRename();
                break;
        }
    }

    private static string? TakeRenameText(object item) =>
        item switch
        {
            WallLayerNodeViewModel layer => layer.TakeRenameText(),
            WallGroupNodeViewModel group => group.TakeRenameText(),
            WallItemViewModel wall => wall.TakeRenameText(),
            WallPortalItemViewModel portal => portal.TakeRenameText(),
            _ => null,
        };

    private static void NotifyDisplayName(object item)
    {
        switch (item)
        {
            case WallLayerNodeViewModel layer:
                layer.RefreshFromModel();
                break;
            case WallGroupNodeViewModel group:
                group.RefreshFromModel();
                break;
            case WallItemViewModel wall:
                wall.RefreshFromModel();
                break;
            case WallPortalItemViewModel portal:
                portal.RefreshFromModel();
                break;
        }
    }

    private static void CancelRenameInTree(object node)
    {
        switch (node)
        {
            case WallLayerNodeViewModel layer:
                layer.CancelRename();
                foreach (var child in layer.Children)
                {
                    CancelRenameInTree(child);
                }

                break;
            case WallGroupNodeViewModel group:
                group.CancelRename();
                foreach (var child in group.Children)
                {
                    CancelRenameInTree(child);
                }

                break;
            case WallItemViewModel wall:
                wall.CancelRename();
                foreach (var portal in wall.Portals)
                {
                    portal.CancelRename();
                }

                break;
            case WallPortalItemViewModel portal:
                portal.CancelRename();
                break;
        }
    }

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
                return;
        }

        UpdateTreeNodeSelection(item, modifiers);
    }

    private void UpdateTreeNodeSelection(object? item, KeyModifiers modifiers)
    {
        if (item is null)
        {
            _selectedTreeNodes.Clear();
            _treeSelectionAnchor = null;
            _syncingSelection = true;
            SelectedTreeItem = null;
            _syncingSelection = false;
            return;
        }

        if (HasMultiSelectModifier(modifiers))
        {
            if (!_selectedTreeNodes.Remove(item))
            {
                _selectedTreeNodes.Add(item);
            }

            _treeSelectionAnchor = item;
        }
        else if (modifiers.HasFlag(KeyModifiers.Shift) && _treeSelectionAnchor is not null)
        {
            var ordered = EnumerateTreeNodesInOrder().ToList();
            var anchorIndex = ordered.FindIndex(node => ReferenceEquals(node, _treeSelectionAnchor));
            var clickIndex = ordered.FindIndex(node => ReferenceEquals(node, item));
            _selectedTreeNodes.Clear();
            if (anchorIndex >= 0 && clickIndex >= 0)
            {
                var start = Math.Min(anchorIndex, clickIndex);
                var end = Math.Max(anchorIndex, clickIndex);
                for (var index = start; index <= end; index++)
                {
                    if (ordered[index].GetType() == item.GetType())
                    {
                        _selectedTreeNodes.Add(ordered[index]);
                    }
                }
            }
            else
            {
                _selectedTreeNodes.Add(item);
            }
        }
        else
        {
            _selectedTreeNodes.Clear();
            _selectedTreeNodes.Add(item);
            _treeSelectionAnchor = item;
        }

        _syncingSelection = true;
        SelectedTreeItem = item;
        _syncingSelection = false;
    }

    private IEnumerable<object> EnumerateTreeNodesInOrder()
    {
        foreach (var layer in Layers)
        {
            yield return layer;
            foreach (var node in EnumerateTreeNodesInOrder(layer.Children))
            {
                yield return node;
            }
        }
    }

    private static IEnumerable<object> EnumerateTreeNodesInOrder(IEnumerable<object> children)
    {
        foreach (var child in children)
        {
            yield return child;
            switch (child)
            {
                case WallGroupNodeViewModel group:
                    foreach (var nested in EnumerateTreeNodesInOrder(group.Children))
                    {
                        yield return nested;
                    }

                    break;
                case WallItemViewModel wall:
                    foreach (var portal in wall.Portals)
                    {
                        yield return portal;
                    }

                    break;
            }
        }
    }

    private static bool HasMultiSelectModifier(KeyModifiers modifiers) =>
        modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);

    private void HandleWallTreeClick(int wallId, WallPortal? portal, KeyModifiers modifiers)
    {
        if (HasMultiSelectModifier(modifiers))
        {
            _session.ToggleWallInSelection(wallId);
            _selectionAnchorWallId = wallId;
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
                return;
            }
        }

        _session.SetSelection([wallId], wallId, portal);
        _selectionAnchorWallId = wallId;
    }

    private void HandleGroupTreeClick(IReadOnlyList<int> wallIds, KeyModifiers modifiers)
    {
        if (wallIds.Count == 0)
        {
            return;
        }

        if (HasMultiSelectModifier(modifiers))
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
            return;
        }

        _session.SetSelection(wallIds, wallIds[0]);
        _selectionAnchorWallId = wallIds[0];
    }

    private void SyncSelectedTreeItemFromPrimary(WallPortal? portal)
    {
        if (_session.FocusedWallEntityId is not int wallId)
        {
            _selectedTreeNodes.Clear();
            _treeSelectionAnchor = null;
            _syncingSelection = true;
            SelectedTreeItem = null;
            _syncingSelection = false;
            return;
        }

        var treeItem = FindTreeItem(wallId, portal ?? _session.FocusedPortal);
        _selectedTreeNodes.Clear();
        if (treeItem is not null)
        {
            _selectedTreeNodes.Add(treeItem);
        }

        _treeSelectionAnchor = treeItem;
        _syncingSelection = true;
        SelectedTreeItem = treeItem;
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
        _selectedTreeNodes.Clear();
        _treeSelectionAnchor = null;
        _renameTargets = null;
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
    private readonly string _fallbackDisplayName;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _renameText = string.Empty;

    public WallLayerNodeViewModel(EditorSession session, string layerId, string displayName)
    {
        _session = session;
        LayerId = layerId;
        _fallbackDisplayName = displayName;
    }

    public string LayerId { get; }

    public string DisplayName
    {
        get
        {
            var layer = _session.Map?.Layers.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, LayerId, StringComparison.OrdinalIgnoreCase));
            return layer?.DisplayName ?? _fallbackDisplayName;
        }
    }

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

    public void BeginRename()
    {
        RenameText = DisplayName;
        IsRenaming = true;
    }

    public string? TakeRenameText()
    {
        if (!IsRenaming)
        {
            return null;
        }

        var text = RenameText;
        IsRenaming = false;
        return text;
    }

    public void CancelRename()
    {
        IsRenaming = false;
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

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _renameText = string.Empty;

    public WallGroupNodeViewModel(EditorSession session, EntityGroup group)
    {
        _session = session;
        Group = group;
    }

    public EntityGroup Group { get; }

    public ObservableCollection<object> Children { get; } = [];

    public string DisplayName => Group.DisplayName;

    public void BeginRename()
    {
        RenameText = DisplayName;
        IsRenaming = true;
    }

    public string? TakeRenameText()
    {
        if (!IsRenaming)
        {
            return null;
        }

        var text = RenameText;
        IsRenaming = false;
        return text;
    }

    public void CancelRename()
    {
        IsRenaming = false;
    }

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

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _renameText = string.Empty;

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

    public void BeginRename()
    {
        RenameText = DisplayName;
        IsRenaming = true;
    }

    public string? TakeRenameText()
    {
        if (!IsRenaming)
        {
            return null;
        }

        var text = RenameText;
        IsRenaming = false;
        return text;
    }

    public void CancelRename()
    {
        IsRenaming = false;
    }

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
                WallLineEditing.EnsureDefaultTerrainThickness(Wall);
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

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _renameText = string.Empty;

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

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Portal.Name) ? $"Portal {PortalNumber}" : Portal.Name;

    public void BeginRename()
    {
        RenameText = DisplayName;
        IsRenaming = true;
    }

    public string? TakeRenameText()
    {
        if (!IsRenaming)
        {
            return null;
        }

        var text = RenameText;
        IsRenaming = false;
        return text;
    }

    public void CancelRename()
    {
        IsRenaming = false;
    }

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
        OnPropertyChanged(nameof(DisplayName));
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
