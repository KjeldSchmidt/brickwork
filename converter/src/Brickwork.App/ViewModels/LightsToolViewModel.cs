using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using IconPacks.Avalonia.FontAwesome6;
using Brickwork.Core.Models;

namespace Brickwork.App.ViewModels;

/// <summary>Lists the map's lights grouped by kind (e.g. "Hand Lantern Lit"), with per-kind settings.</summary>
public partial class LightsToolViewModel : Tool
{
    public const string LightDragFormat = "application/x-brickwork-light-ids";

    private readonly EditorSession _session;
    private readonly List<string> _emptyKindNames = [];
    private readonly Dictionary<string, LightKindPlacementDefaults> _emptyKindDrafts =
        new(StringComparer.OrdinalIgnoreCase);
    private int? _selectionAnchorEntityId;
    private IReadOnlyList<object>? _renameTargets;

    [ObservableProperty]
    private ObservableCollection<LightKindViewModel> _kinds = [];

    [ObservableProperty]
    private object? _selectedTreeItem;

    [ObservableProperty]
    private int _treeRevision;

    public LightsToolViewModel(EditorSession session)
    {
        _session = session;
        _session.ResolveLightKindPlacementDefaults = TryGetKindPlacementDefaults;
        _session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(EditorSession.Map))
            {
                _emptyKindNames.Clear();
                _emptyKindDrafts.Clear();
                _selectionAnchorEntityId = null;
                Rebuild(clearSelection: true);
            }

            if (args.PropertyName is nameof(EditorSession.ContentRevision) && !_session.IsGestureActive)
            {
                Sync();
            }

            if (args.PropertyName is nameof(EditorSession.TreeFocusGeneration)
                or nameof(EditorSession.FocusedLightEntityId))
            {
                SyncSelectionFromSession();
            }

            if (args.PropertyName is nameof(EditorSession.HighlightRevision))
            {
                RefreshHighlights();
            }
        };
        Rebuild(clearSelection: true);
    }

    public bool HasMap => _session.Map is not null;

    public bool HasLights => Kinds.Any(kind => kind.Lights.Count > 0);

    public bool ShowEmptyMessage => !HasMap;

    public bool ShowPanelContent => HasMap;

    public string EmptyMessage => "Open a Source Map to see lights";

    public string Summary
    {
        get
        {
            var total = Kinds.Sum(kind => kind.Lights.Count);
            var active = Kinds.Sum(kind => kind.Lights.Count(light => light.IsActive));
            return $"{active} of {total} lights exported. Radii are in grid squares.";
        }
    }

    public void SetHoveredItem(object? item) =>
        _session.SetHoveredLight(item is LightItemViewModel light ? light.Light : null);

    public bool IsTreeNodeSelected(object? item) =>
        item is LightItemViewModel light &&
        _session.SelectedLightEntityIds.Contains(light.Light.EntityId);

    public void FocusTreeItem(object? item)
    {
        if (item is null)
        {
            return;
        }

        SelectedTreeItem = item;
    }

    public void SelectLight(LightItemViewModel light, bool ctrl, bool shift)
    {
        _session.ActiveMapTool = MapToolKind.LightEditing;
        SetActiveKind(light.Kind.Name);

        if (shift)
        {
            SelectLightRange(light);
        }
        else if (ctrl)
        {
            if (CanExtendSelectionWith(light.Kind))
            {
                _session.ToggleLightInSelection(light.Light.EntityId);
            }
            else
            {
                _session.SetLightSelection([light.Light.EntityId], light.Light.EntityId);
            }

            _selectionAnchorEntityId = light.Light.EntityId;
        }
        else
        {
            _session.SetLightSelection([light.Light.EntityId], light.Light.EntityId);
            _selectionAnchorEntityId = light.Light.EntityId;
        }

        FocusTreeItem(light);
        RefreshHighlights();
        TreeRevision++;
    }

    public void SelectKind(LightKindViewModel kind)
    {
        _session.ActiveMapTool = MapToolKind.LightEditing;
        SetActiveKind(kind.Name);
        var ids = kind.Lights.Select(light => light.EntityId).ToList();
        if (ids.Count == 0)
        {
            _session.ClearLightSelection();
            _selectionAnchorEntityId = null;
            FocusTreeItem(kind);
            RefreshHighlights();
            TreeRevision++;
            return;
        }

        _session.SetLightSelection(ids, ids[0]);
        _selectionAnchorEntityId = ids[0];
        FocusTreeItem(kind.Children.FirstOrDefault() ?? (object)kind);
        RefreshHighlights();
        TreeRevision++;
    }

    partial void OnSelectedTreeItemChanged(object? value) => _ = value;

    public IReadOnlyList<int> GetLightsToMove(LightItemViewModel source)
    {
        if (_session.SelectedLightEntityIds.Contains(source.Light.EntityId))
        {
            return source.Kind.Lights
                .Where(light => _session.SelectedLightEntityIds.Contains(light.EntityId))
                .Select(light => light.EntityId)
                .ToList();
        }

        return [source.Light.EntityId];
    }

    public void MoveLightsToKind(IReadOnlyList<int> lightIds, LightKindViewModel targetKind)
    {
        if (_session.Map is null || lightIds.Count == 0)
        {
            return;
        }

        var map = _session.Map;
        var lights = map.Lights
            .Where(light => lightIds.Contains(light.EntityId))
            .Where(light => !string.Equals(light.KindName, targetKind.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (lights.Count == 0)
        {
            return;
        }

        var targetName = targetKind.Name;
        var emptiedKinds = lights
            .Select(light => light.KindName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(name => map.Lights.Count(candidate =>
                lightIds.Contains(candidate.EntityId) == false &&
                string.Equals(candidate.KindName, name, StringComparison.OrdinalIgnoreCase)) == 0)
            .ToList();

        _session.Execute(lights.Count == 1 ? "Move light" : "Move lights", () =>
        {
            foreach (var light in lights)
            {
                map.Lights.Remove(light);
            }

            foreach (var light in lights)
            {
                light.SourceName = targetName;
            }

            AppendLightsToKind(map, lights, targetName);
        });

        foreach (var emptied in emptiedKinds)
        {
            AddEmptyKind(emptied);
        }

        RemoveEmptyKind(targetName);
        SetActiveKind(targetName);
        targetKind.IsInstancesExpanded = true;
        Rebuild(clearSelection: false);
        _session.SetLightSelection(lightIds, lightIds[0]);
        _selectionAnchorEntityId = lightIds[0];
        SyncSelectionFromSession();
    }

    public bool CanRename(object? item) => item is LightKindViewModel or LightItemViewModel;

    public bool BeginRenameSelection()
    {
        var primary = SelectedTreeItem;
        if (!CanRename(primary))
        {
            primary = Kinds.SelectMany(kind => kind.Children)
                .FirstOrDefault(item => _session.SelectedLightEntityIds.Contains(item.Light.EntityId));
        }

        if (primary is null || !CanRename(primary))
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

        _renameTargets = ResolveRenameTargets(item!);
        switch (item)
        {
            case LightKindViewModel kind:
                kind.BeginRename();
                FocusTreeItem(kind);
                break;
            case LightItemViewModel light:
                light.BeginRename();
                FocusTreeItem(light);
                break;
        }

        TreeRevision++;
    }

    public void CommitRename(object? item)
    {
        if (item is null)
        {
            return;
        }

        var renameText = item switch
        {
            LightKindViewModel kind => kind.TakeRenameText(),
            LightItemViewModel light => light.TakeRenameText(),
            _ => null,
        };
        var targets = _renameTargets ?? ResolveRenameTargets(item);
        _renameTargets = null;
        CancelRename();

        if (renameText is null)
        {
            return;
        }

        var name = string.IsNullOrWhiteSpace(renameText) ? null : renameText.Trim();
        if (targets.Count == 0)
        {
            return;
        }

        if (targets[0] is LightKindViewModel)
        {
            RenameKinds(targets.OfType<LightKindViewModel>().ToList(), name);
            return;
        }

        RenameInstances(targets.OfType<LightItemViewModel>().ToList(), name);
    }

    public void CancelRename()
    {
        _renameTargets = null;
        foreach (var kind in Kinds)
        {
            kind.CancelRename();
            foreach (var child in kind.Children)
            {
                child.CancelRename();
            }
        }
    }

    [RelayCommand]
    private void CreateLightGroup()
    {
        if (_session.Map is null)
        {
            return;
        }

        var name = CreateUniqueKindName("Light Group");
        AddEmptyKind(name);
        Rebuild(clearSelection: false);
        var kind = Kinds.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
        if (kind is null)
        {
            return;
        }

        SetActiveKind(name);
        kind.IsSettingsExpanded = true;
        kind.IsInstancesExpanded = true;
        _session.ClearLightSelection();
        _selectionAnchorEntityId = null;
        FocusTreeItem(kind);
        RefreshHighlights();
        BeginRename(kind);
    }

    private void SelectLightRange(LightItemViewModel light)
    {
        var kind = light.Kind;
        var children = kind.Children;
        var anchor = _selectionAnchorEntityId is int anchorId
            ? children.FirstOrDefault(item => item.Light.EntityId == anchorId)
            : null;
        if (anchor is null || !ReferenceEquals(anchor.Kind, kind))
        {
            _session.SetLightSelection([light.Light.EntityId], light.Light.EntityId);
            _selectionAnchorEntityId = light.Light.EntityId;
            return;
        }

        var start = children.IndexOf(anchor);
        var end = children.IndexOf(light);
        if (start < 0 || end < 0)
        {
            _session.SetLightSelection([light.Light.EntityId], light.Light.EntityId);
            _selectionAnchorEntityId = light.Light.EntityId;
            return;
        }

        if (start > end)
        {
            (start, end) = (end, start);
        }

        var ids = children.Skip(start).Take(end - start + 1).Select(item => item.Light.EntityId).ToList();
        _session.SetLightSelection(ids, light.Light.EntityId);
    }

    private IReadOnlyList<object> ResolveRenameTargets(object item) =>
        item switch
        {
            LightItemViewModel light =>
                light.Kind.Children
                    .Where(candidate => _session.SelectedLightEntityIds.Contains(candidate.Light.EntityId))
                    .Cast<object>()
                    .DefaultIfEmpty(light)
                    .ToList(),
            LightKindViewModel kind => [kind],
            _ => [],
        };

    private void RenameInstances(IReadOnlyList<LightItemViewModel> items, string? name)
    {
        if (items.Count == 0)
        {
            return;
        }

        var display = string.IsNullOrWhiteSpace(name) ? null : name;
        _session.Execute(items.Count == 1 ? "Rename light" : "Rename lights", () =>
        {
            foreach (var item in items)
            {
                item.Light.Name = display ?? item.Light.KindName;
            }
        });

        foreach (var item in items)
        {
            item.RefreshFromModel();
        }

        NotifySummaryChanged();
        TreeRevision++;
    }

    private void RenameKinds(IReadOnlyList<LightKindViewModel> kinds, string? name)
    {
        if (kinds.Count == 0 || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var newName = name.Trim();
        foreach (var kind in kinds)
        {
            var oldName = kind.Name;
            if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (kind.Lights.Count == 0)
            {
                RenameEmptyKind(oldName, newName);
                continue;
            }

            _session.Execute("Rename light kind", () =>
            {
                foreach (var light in kind.Lights)
                {
                    if (string.Equals(light.Name, oldName, StringComparison.OrdinalIgnoreCase) ||
                        string.IsNullOrWhiteSpace(light.Name))
                    {
                        light.Name = newName;
                    }

                    light.SourceName = newName;
                }
            });

            RemoveEmptyKind(oldName);
        }

        SetActiveKind(newName);
        Rebuild(clearSelection: false);
    }

    private string CreateUniqueKindName(string baseName)
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kind in Kinds)
        {
            existing.Add(kind.Name);
        }

        foreach (var empty in _emptyKindNames)
        {
            existing.Add(empty);
        }

        if (!existing.Contains(baseName))
        {
            return baseName;
        }

        for (var i = 2; ; i++)
        {
            var candidate = $"{baseName} {i}";
            if (!existing.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    internal LightKindPlacementDefaults CreateDefaultDraft() =>
        new(
            _session.LightBrightRadius,
            Math.Max(_session.LightDimRadius, _session.LightBrightRadius),
            LightSource.DefaultColor,
            LightAnimation.None,
            LightAnimations.DefaultSpeed,
            LightAnimations.DefaultIntensity);

    internal LightKindPlacementDefaults GetOrCreateDraft(string kindName)
    {
        if (_emptyKindDrafts.TryGetValue(kindName, out var draft))
        {
            return draft;
        }

        draft = CreateDefaultDraft();
        _emptyKindDrafts[kindName] = draft;
        return draft;
    }

    internal void SaveDraft(string kindName, LightKindPlacementDefaults draft) =>
        _emptyKindDrafts[kindName] = draft;

    public LightKindPlacementDefaults? TryGetKindPlacementDefaults(string kindName)
    {
        if (string.IsNullOrWhiteSpace(kindName))
        {
            return null;
        }

        var kind = Kinds.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, kindName, StringComparison.OrdinalIgnoreCase));
        if (kind is { Lights.Count: > 0 })
        {
            var template = kind.Lights[0];
            return new LightKindPlacementDefaults(
                template.BrightRadius,
                template.DimRadius,
                LightColors.Normalize(template.Color),
                template.Animation,
                template.AnimationSpeed,
                template.AnimationIntensity);
        }

        return GetOrCreateDraft(kindName);
    }

    private bool HasEmptyKind(string name) =>
        _emptyKindNames.Any(candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase));

    private void AddEmptyKind(string name)
    {
        if (HasEmptyKind(name))
        {
            return;
        }

        _emptyKindNames.Add(name);
        GetOrCreateDraft(name);
    }

    private void RemoveEmptyKind(string name)
    {
        _emptyKindNames.RemoveAll(candidate =>
            string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase));
        // Keep draft until map change so rename/undo can still read it; drop when populated.
        if (_session.Map?.Lights.Any(light =>
                string.Equals(light.KindName, name, StringComparison.OrdinalIgnoreCase)) == true)
        {
            RemoveDraft(name);
        }
    }

    private void RemoveDraft(string name)
    {
        foreach (var key in _emptyKindDrafts.Keys
                     .Where(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            _emptyKindDrafts.Remove(key);
        }
    }

    private void RenameEmptyKind(string oldName, string newName)
    {
        var index = _emptyKindNames.FindIndex(candidate =>
            string.Equals(candidate, oldName, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _emptyKindNames[index] = newName;
        }
        else
        {
            _emptyKindNames.Add(newName);
        }

        if (_emptyKindDrafts.TryGetValue(oldName, out var draft))
        {
            RemoveDraft(oldName);
            _emptyKindDrafts[newName] = draft;
        }
        else
        {
            GetOrCreateDraft(newName);
        }
    }

    private static void AppendLightsToKind(MapDocument map, IReadOnlyList<LightSource> lights, string targetKindName)
    {
        var insertAt = map.Lights.Count;
        for (var i = map.Lights.Count - 1; i >= 0; i--)
        {
            if (string.Equals(map.Lights[i].KindName, targetKindName, StringComparison.OrdinalIgnoreCase))
            {
                insertAt = i + 1;
                break;
            }
        }

        for (var offset = 0; offset < lights.Count; offset++)
        {
            map.Lights.Insert(insertAt + offset, lights[offset]);
        }
    }

    private bool CanExtendSelectionWith(LightKindViewModel kind)
    {
        var selected = _session.SelectedLightEntityIds;
        if (selected.Count == 0)
        {
            return true;
        }

        return Kinds.SelectMany(candidate => candidate.Children)
            .Where(item => selected.Contains(item.Light.EntityId))
            .All(item => ReferenceEquals(item.Kind, kind));
    }

    private void SetActiveKind(string kindName)
    {
        if (!string.IsNullOrWhiteSpace(kindName))
        {
            _session.ActiveLightKindName = kindName;
        }
    }

    private void Rebuild(bool clearSelection)
    {
        var selectedIds = clearSelection
            ? null
            : _session.SelectedLightEntityIds.ToHashSet();
        var focusedId = clearSelection ? null : _session.FocusedLightEntityId;
        var settingsExpanded = Kinds.ToDictionary(
            kind => kind.Name,
            kind => kind.IsSettingsExpanded,
            StringComparer.OrdinalIgnoreCase);
        var instancesExpanded = Kinds.ToDictionary(
            kind => kind.Name,
            kind => kind.IsInstancesExpanded,
            StringComparer.OrdinalIgnoreCase);

        SelectedTreeItem = null;
        if (clearSelection)
        {
            _session.ClearLightSelection();
            _selectionAnchorEntityId = null;
        }

        var populated = new List<LightKindViewModel>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (_session.Map is { } map)
        {
            foreach (var group in map.Lights
                         .GroupBy(light => light.KindName, StringComparer.OrdinalIgnoreCase)
                         .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
            {
                seen.Add(group.Key);
                RemoveEmptyKind(group.Key);
                populated.Add(new LightKindViewModel(this, _session, group.Key, group.ToList())
                {
                    IsSettingsExpanded = settingsExpanded.GetValueOrDefault(group.Key, false),
                    IsInstancesExpanded = instancesExpanded.GetValueOrDefault(group.Key, false),
                });
            }
        }

        var empties = new List<LightKindViewModel>();
        foreach (var emptyName in _emptyKindNames.ToList())
        {
            if (!seen.Add(emptyName))
            {
                continue;
            }

            empties.Add(new LightKindViewModel(this, _session, emptyName, [], GetOrCreateDraft(emptyName))
            {
                IsSettingsExpanded = settingsExpanded.GetValueOrDefault(emptyName, false),
                IsInstancesExpanded = instancesExpanded.GetValueOrDefault(emptyName, false),
            });
        }

        // Populated A–Z, then empty shells in creation order (no merge-sort).
        Kinds = new ObservableCollection<LightKindViewModel>(populated.Concat(empties));

        if (selectedIds is { Count: > 0 })
        {
            var presentIds = _session.Map?.Lights.Select(light => light.EntityId).ToHashSet() ?? [];
            var stillPresent = selectedIds.Where(presentIds.Contains).ToList();
            if (stillPresent.Count > 0)
            {
                var primary = focusedId is int id && stillPresent.Contains(id) ? id : stillPresent[0];
                _session.SetLightSelection(stillPresent, primary);
                _selectionAnchorEntityId = primary;
            }
            else if (!clearSelection)
            {
                _session.ClearLightSelection();
                _selectionAnchorEntityId = null;
            }
        }

        TreeRevision++;
        OnPropertyChanged(nameof(HasMap));
        OnPropertyChanged(nameof(HasLights));
        OnPropertyChanged(nameof(ShowEmptyMessage));
        OnPropertyChanged(nameof(ShowPanelContent));
        OnPropertyChanged(nameof(EmptyMessage));
        OnPropertyChanged(nameof(Summary));
        RefreshHighlights();
    }

    private void Sync()
    {
        var map = _session.Map;
        var shown = Kinds.SelectMany(kind => kind.Lights).ToList();
        var regroupNeeded = map is not null &&
            (Kinds.Any(kind => kind.Lights.Any(light =>
                 !string.Equals(light.KindName, kind.Name, StringComparison.OrdinalIgnoreCase))) ||
             Kinds.Any(kind => kind.Lights.Count == 0 && !HasEmptyKind(kind.Name) &&
                 map.Lights.All(light => !string.Equals(light.KindName, kind.Name, StringComparison.OrdinalIgnoreCase))));
        if (map is null ||
            regroupNeeded ||
            shown.Count != map.Lights.Count ||
            !shown.Select(light => light.EntityId).ToHashSet().SetEquals(map.Lights.Select(light => light.EntityId)) ||
            shown.Any(light => !map.Lights.Contains(light)))
        {
            Rebuild(clearSelection: false);
            return;
        }

        foreach (var kind in Kinds)
        {
            kind.RefreshFromModel();
        }

        OnPropertyChanged(nameof(Summary));
        SyncSelectionFromSession();
    }

    private void SyncSelectionFromSession()
    {
        var focusedId = _session.FocusedLightEntityId;
        object? next = null;
        if (focusedId is int id)
        {
            next = Kinds.SelectMany(kind => kind.Children)
                .FirstOrDefault(item => item.Light.EntityId == id);
            if (next is LightItemViewModel item)
            {
                item.Kind.IsInstancesExpanded = true;
                SetActiveKind(item.Kind.Name);
                _selectionAnchorEntityId ??= id;
            }
        }

        if (!ReferenceEquals(SelectedTreeItem, next) && next is not null)
        {
            SelectedTreeItem = next;
        }

        RefreshHighlights();
        TreeRevision++;
    }

    internal void ClearSelectionIfInKind(LightKindViewModel kind)
    {
        var selected = _session.SelectedLightEntityIds;
        if (!kind.Lights.Any(light => selected.Contains(light.EntityId)))
        {
            return;
        }

        SelectedTreeItem = null;
        _session.ClearLightSelection();
        _selectionAnchorEntityId = null;
        RefreshHighlights();
        TreeRevision++;
    }

    internal void RefreshHighlights()
    {
        foreach (var kind in Kinds)
        {
            kind.RefreshDropHighlight();
            kind.RefreshHighlightState();
            foreach (var child in kind.Children)
            {
                child.RefreshHighlightState();
            }
        }
    }

    internal void NotifySummaryChanged() => OnPropertyChanged(nameof(Summary));
}

public partial class LightKindViewModel : ObservableObject
{
    private readonly LightsToolViewModel _tool;
    private readonly EditorSession _session;
    private LightKindPlacementDefaults _draft;

    [ObservableProperty]
    private bool _isSettingsExpanded;

    [ObservableProperty]
    private bool _isInstancesExpanded;

    [ObservableProperty]
    private bool _isDropTarget;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _renameText = string.Empty;

    public LightKindViewModel(
        LightsToolViewModel tool,
        EditorSession session,
        string name,
        IReadOnlyList<LightSource> lights,
        LightKindPlacementDefaults? draft = null)
    {
        _tool = tool;
        _session = session;
        Name = name;
        Lights = lights;
        _draft = draft ?? tool.CreateDefaultDraft();
        Children = new ObservableCollection<LightItemViewModel>(
            lights.Select((light, index) => new LightItemViewModel(tool, this, session, light, index + 1)));
    }

    public string Name { get; }

    public IReadOnlyList<LightSource> Lights { get; }

    public ObservableCollection<LightItemViewModel> Children { get; }

    public string Header => $"{Name} ({Lights.Count})";

    public bool IsSelected =>
        Lights.Count > 0
            ? Lights.All(light => _session.SelectedLightEntityIds.Contains(light.EntityId))
            : string.Equals(_session.ActiveLightKindName, Name, StringComparison.OrdinalIgnoreCase);

    public PackIconFontAwesome6Kind SettingsExpandIconKind =>
        IsSettingsExpanded ? PackIconFontAwesome6Kind.AngleDownSolid : PackIconFontAwesome6Kind.AngleRightSolid;

    public PackIconFontAwesome6Kind InstancesExpandIconKind =>
        IsInstancesExpanded ? PackIconFontAwesome6Kind.AngleDownSolid : PackIconFontAwesome6Kind.AngleRightSolid;

    [RelayCommand]
    private void ToggleSettingsExpanded() => IsSettingsExpanded = !IsSettingsExpanded;

    [RelayCommand]
    private void ToggleInstancesExpanded() => IsInstancesExpanded = !IsInstancesExpanded;

    partial void OnIsSettingsExpandedChanged(bool value) =>
        OnPropertyChanged(nameof(SettingsExpandIconKind));

    partial void OnIsInstancesExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(InstancesExpandIconKind));
        if (!value)
        {
            _tool.ClearSelectionIfInKind(this);
        }
    }

    public void BeginRename()
    {
        RenameText = Name;
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

    public void CancelRename() => IsRenaming = false;

    public void RefreshDropHighlight() => OnPropertyChanged(nameof(IsDropTarget));

    public void RefreshHighlightState() => OnPropertyChanged(nameof(IsSelected));

    public bool? IsActive
    {
        get => Lights.Count == 0
            ? false
            : Lights.All(light => light.IsActive) ? true : Lights.Any(light => light.IsActive) ? null : false;
        set
        {
            if (Lights.Count == 0)
            {
                return;
            }

            var enabled = value ?? true;
            Apply(enabled ? "Enable lights" : "Disable lights", light => light.IsActive = enabled);
        }
    }

    public decimal? BrightRadius
    {
        get => Lights.Count == 0
            ? Math.Round((decimal)_draft.BrightRadius, 2)
            : CommonValue(light => light.BrightRadius);
        set
        {
            if (value is decimal radius)
            {
                Apply("Change bright radius", light =>
                {
                    light.BrightRadius = (double)radius;
                    if (light.DimRadius < light.BrightRadius)
                    {
                        light.DimRadius = light.BrightRadius;
                    }
                }, draft => draft with
                {
                    BrightRadius = (double)radius,
                    DimRadius = Math.Max(draft.DimRadius, (double)radius),
                });
            }
        }
    }

    public double BrightRadiusSlider
    {
        get => (double)(BrightRadius ?? (decimal)_draft.BrightRadius);
        set => BrightRadius = (decimal)Math.Round(Math.Clamp(value, 0, 40), 1);
    }

    public decimal? DimRadius
    {
        get => Lights.Count == 0
            ? Math.Round((decimal)_draft.DimRadius, 2)
            : CommonValue(light => light.DimRadius);
        set
        {
            if (value is decimal radius)
            {
                Apply("Change dim radius", light =>
                    light.DimRadius = Math.Max((double)radius, light.BrightRadius),
                    draft => draft with
                    {
                        DimRadius = Math.Max((double)radius, draft.BrightRadius),
                    });
            }
        }
    }

    public double DimRadiusSlider
    {
        get => (double)(DimRadius ?? (decimal)_draft.DimRadius);
        set => DimRadius = (decimal)Math.Round(Math.Clamp(value, 0, 40), 1);
    }

    public string Color
    {
        get => Lights.Count == 0
            ? LightColors.Normalize(_draft.Color)
            : Lights.Count > 0 &&
              Lights.Select(light => LightColors.Normalize(light.Color)).Distinct().Count() == 1
                ? LightColors.Normalize(Lights[0].Color)
                : string.Empty;
        set
        {
            if (LightColors.TryParse(value, out _, out _, out _))
            {
                var color = LightColors.Normalize(value);
                Apply("Change light color", light => light.Color = color,
                    draft => draft with { Color = color });
            }
        }
    }

    public Color PickerColor
    {
        get
        {
            var hex = string.IsNullOrEmpty(Color) ? _draft.Color : Color;
            return LightColors.TryParse(hex, out var red, out var green, out var blue)
                ? Avalonia.Media.Color.FromRgb(red, green, blue)
                : Avalonia.Media.Color.FromRgb(0xff, 0xb1, 0x5c);
        }
        set
        {
            var hex = $"#{value.R:x2}{value.G:x2}{value.B:x2}";
            if (!string.Equals(Color, LightColors.Normalize(hex), StringComparison.OrdinalIgnoreCase))
            {
                Color = hex;
            }
        }
    }

    public IBrush ColorBrush => new SolidColorBrush(PickerColor);

    public IReadOnlyList<LightAnimation> AnimationOptions => LightAnimations.All;

    public LightAnimation? Animation
    {
        get => Lights.Count == 0
            ? _draft.Animation
            : Lights.Count > 0 && Lights.Select(light => light.Animation).Distinct().Count() == 1
                ? Lights[0].Animation
                : null;
        set
        {
            if (value is LightAnimation animation && animation != Animation)
            {
                Apply("Change light animation", light => light.Animation = animation,
                    draft => draft with { Animation = animation });
            }
        }
    }

    public bool HasAnimation => Animation is not (LightAnimation.None or null);

    public decimal? AnimationSpeed
    {
        get => Lights.Count == 0
            ? _draft.AnimationSpeed
            : CommonValue(light => light.AnimationSpeed);
        set
        {
            if (value is decimal speed)
            {
                var clamped = (int)Math.Clamp(speed, 1, 10);
                Apply("Change animation speed", light => light.AnimationSpeed = clamped,
                    draft => draft with { AnimationSpeed = clamped });
            }
        }
    }

    public decimal? AnimationIntensity
    {
        get => Lights.Count == 0
            ? _draft.AnimationIntensity
            : CommonValue(light => light.AnimationIntensity);
        set
        {
            if (value is decimal intensity)
            {
                var clamped = (int)Math.Clamp(intensity, 1, 10);
                Apply("Change animation intensity", light => light.AnimationIntensity = clamped,
                    draft => draft with { AnimationIntensity = clamped });
            }
        }
    }

    public void RefreshFromModel()
    {
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(Header));
        OnPropertyChanged(nameof(BrightRadius));
        OnPropertyChanged(nameof(BrightRadiusSlider));
        OnPropertyChanged(nameof(DimRadius));
        OnPropertyChanged(nameof(DimRadiusSlider));
        OnPropertyChanged(nameof(Color));
        OnPropertyChanged(nameof(PickerColor));
        OnPropertyChanged(nameof(ColorBrush));
        OnPropertyChanged(nameof(Animation));
        OnPropertyChanged(nameof(HasAnimation));
        OnPropertyChanged(nameof(AnimationSpeed));
        OnPropertyChanged(nameof(AnimationIntensity));
        RefreshHighlightState();
        foreach (var child in Children)
        {
            child.RefreshFromModel();
        }
    }

    private decimal? CommonValue(Func<LightSource, double> selector)
    {
        if (Lights.Count == 0)
        {
            return null;
        }

        var values = Lights.Select(selector).Distinct().ToList();
        return values.Count == 1 ? Math.Round((decimal)values[0], 2) : null;
    }

    private void Apply(
        string name,
        Action<LightSource> mutate,
        Func<LightKindPlacementDefaults, LightKindPlacementDefaults>? updateDraft = null)
    {
        if (Lights.Count == 0)
        {
            if (updateDraft is not null)
            {
                _draft = updateDraft(_draft);
                _tool.SaveDraft(Name, _draft);
                RefreshFromModel();
            }

            return;
        }

        _session.Execute(name, () =>
        {
            foreach (var light in Lights)
            {
                mutate(light);
            }
        });
        RefreshFromModel();
        _tool.NotifySummaryChanged();
    }
}

public partial class LightItemViewModel : ObservableObject
{
    private readonly LightsToolViewModel _tool;
    private readonly EditorSession _session;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _renameText = string.Empty;

    public LightItemViewModel(
        LightsToolViewModel tool,
        LightKindViewModel kind,
        EditorSession session,
        LightSource light,
        int number)
    {
        _tool = tool;
        Kind = kind;
        _session = session;
        Light = light;
        Number = number;
    }

    public LightKindViewModel Kind { get; }

    public LightSource Light { get; }

    public int Number { get; }

    public string DisplayName => string.Equals(Light.DisplayName, Light.KindName, StringComparison.OrdinalIgnoreCase)
        ? $"#{Number}"
        : $"#{Number} {Light.DisplayName}";

    public bool IsSelected => _session.SelectedLightEntityIds.Contains(Light.EntityId);

    public bool IsActive
    {
        get => Light.IsActive;
        set
        {
            if (Light.IsActive == value)
            {
                return;
            }

            _session.Execute(value ? "Enable light" : "Disable light", () => Light.IsActive = value);
            OnPropertyChanged();
            _tool.NotifySummaryChanged();
        }
    }

    public void BeginRename()
    {
        RenameText = Light.DisplayName;
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

    public void CancelRename() => IsRenaming = false;

    public void RefreshFromModel()
    {
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(DisplayName));
        RefreshHighlightState();
    }

    public void RefreshHighlightState() => OnPropertyChanged(nameof(IsSelected));
}
