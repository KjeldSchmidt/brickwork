using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;
using Brickwork.Core.Models;

namespace Brickwork.App.ViewModels;

public partial class RegionsToolViewModel : Tool
{
    private readonly EditorSession _session;
    private readonly HashSet<object> _selectedTreeNodes = new(ReferenceEqualityComparer.Instance);
    private object? _renameTarget;

    [ObservableProperty]
    private ObservableCollection<RegionItemViewModel> _regions = [];

    [ObservableProperty]
    private object? _selectedTreeItem;

    [ObservableProperty]
    private int _treeRevision;

    public bool HasRegions => Regions.Count > 0;

    public bool ShowEmptyMessage => !HasRegions;

    public string EmptyMessage => _session.Map is null
        ? "Open a Source Map to see regions"
        : "No regions yet. Use the Region tool (R) and Middle-Drag Empty to draw one.";

    public RegionsToolViewModel(EditorSession session)
    {
        _session = session;
        _session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(EditorSession.Map))
            {
                RebuildRegions();
            }

            if (args.PropertyName is nameof(EditorSession.ContentRevision))
            {
                SyncWithRegionSet();
            }

            if (args.PropertyName is nameof(EditorSession.TreeFocusGeneration))
            {
                ApplyTreeFocusFromSession();
            }

            if (args.PropertyName is nameof(EditorSession.HighlightRevision)
                or nameof(EditorSession.FocusedRegionEntityId)
                or nameof(EditorSession.HoveredRegionEntityId))
            {
                if (args.PropertyName is nameof(EditorSession.FocusedRegionEntityId)
                    && _session.FocusedRegionEntityId is null
                    && SelectedTreeItem is not null)
                {
                    SelectedTreeItem = null;
                }

                RefreshHighlightStates();
            }
        };
        RebuildRegions();
    }

    public void SetHoveredTreeItem(object? item)
    {
        if (item is RegionItemViewModel regionItem)
        {
            _session.SetHoveredRegion(regionItem.Region);
            return;
        }

        _session.ClearHoveredRegion();
    }

    public void ClearTreeHover() => _session.ClearHoveredRegion();

    public void ClearSelection()
    {
        SelectedTreeItem = null;
        _selectedTreeNodes.Clear();
        _session.ClearRegionSelection();
    }

    public bool IsTreeNodeSelected(object? item) =>
        item is RegionItemViewModel region &&
        _session.SelectedRegionEntityIds.Contains(region.Region.EntityId);

    public void FocusTreeItem(object? item)
    {
        if (item is null)
        {
            return;
        }

        SelectedTreeItem = item;
    }

    public bool DeleteSelectedRegions() => _session.DeleteSelectedRegions();

    public bool CanRename(object? item) => item is RegionItemViewModel;

    public bool CanDelete(object? item) => item is RegionItemViewModel;

    public bool CanConvertToWall(object? item) =>
        item is RegionItemViewModel regionItem && regionItem.Region.Points.Count >= 3;

    public IReadOnlyList<object> GetEditTargets(object? item)
    {
        if (item is null || (!CanRename(item) && !CanDelete(item)))
        {
            return [];
        }

        if (item is RegionItemViewModel region &&
            _session.SelectedRegionEntityIds.Contains(region.Region.EntityId) &&
            _session.SelectedRegionEntityIds.Count > 1)
        {
            return Regions
                .Where(candidate => _session.SelectedRegionEntityIds.Contains(candidate.Region.EntityId))
                .Cast<object>()
                .ToList();
        }

        return [item];
    }

    public bool DeleteTreeItem(object? item)
    {
        if (_session.Map is null || item is null)
        {
            return false;
        }

        var targets = GetEditTargets(item).OfType<RegionItemViewModel>().ToList();
        if (targets.Count == 0)
        {
            return false;
        }

        var deleted = false;
        _session.Execute(targets.Count == 1 ? "Delete region" : "Delete regions", () =>
        {
            foreach (var target in targets)
            {
                deleted |= RegionEditing.RemoveFromMap(_session.Map, target.Region);
            }
        });

        if (deleted)
        {
            ClearSelection();
        }

        return deleted;
    }

    public bool ConvertToWall(object? item)
    {
        if (_session.Map is null || item is not RegionItemViewModel regionItem)
        {
            return false;
        }

        Wall? wall = null;
        _session.Execute("Convert region to wall", () =>
        {
            RegionConversion.TryConvertRegionToWall(_session.Map, regionItem.Region, out wall);
        });

        if (wall is null)
        {
            return false;
        }

        ClearSelection();
        _session.ActiveMapTool = MapToolKind.WallEditing;
        _session.RequestWallTreeFocus(wall);
        return true;
    }

    public bool BeginRenameSelection()
    {
        var primary = SelectedTreeItem;
        if (!CanRename(primary))
        {
            primary = Regions.FirstOrDefault(CanRename);
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
        if (!CanRename(item) || item is not RegionItemViewModel region)
        {
            return;
        }

        _renameTarget = region;
        region.BeginRename();
    }

    public void CommitRename(object? item)
    {
        if (item is not RegionItemViewModel region)
        {
            return;
        }

        var renameText = region.TakeRenameText();
        CancelRename();
        if (renameText is null || _session.Map is null)
        {
            return;
        }

        var name = string.IsNullOrWhiteSpace(renameText) ? null : renameText.Trim();
        if (region.Region.Name == name)
        {
            region.RefreshFromModel();
            return;
        }

        _session.Execute("Rename region", () =>
        {
            region.Region.Name = name;
        });
        region.RefreshFromModel();
    }

    public void CancelRename()
    {
        if (_renameTarget is RegionItemViewModel region)
        {
            region.CancelRename();
        }

        _renameTarget = null;
    }

    public void SelectRegion(RegionItemViewModel region, bool addToSelection)
    {
        if (addToSelection)
        {
            _session.ToggleRegionInSelection(region.Region.EntityId);
        }
        else
        {
            _session.SetRegionSelection([region.Region.EntityId], region.Region.EntityId);
        }

        FocusTreeItem(region);
        TreeRevision++;
    }

    private void RebuildRegions()
    {
        Regions.Clear();
        if (_session.Map is null)
        {
            OnPropertyChanged(nameof(HasRegions));
            OnPropertyChanged(nameof(ShowEmptyMessage));
            OnPropertyChanged(nameof(EmptyMessage));
            TreeRevision++;
            return;
        }

        foreach (var region in _session.Map.Regions.OrderBy(candidate => candidate.EntityId))
        {
            Regions.Add(new RegionItemViewModel(this, _session, region));
        }

        OnPropertyChanged(nameof(HasRegions));
        OnPropertyChanged(nameof(ShowEmptyMessage));
        OnPropertyChanged(nameof(EmptyMessage));
        TreeRevision++;
    }

    private void SyncWithRegionSet()
    {
        if (_session.Map is null)
        {
            RebuildRegions();
            return;
        }

        var existingById = Regions.ToDictionary(item => item.Region.EntityId);
        var nextIds = _session.Map.Regions.Select(region => region.EntityId).ToHashSet();

        for (var index = Regions.Count - 1; index >= 0; index--)
        {
            if (!nextIds.Contains(Regions[index].Region.EntityId))
            {
                Regions.RemoveAt(index);
            }
        }

        foreach (var region in _session.Map.Regions.OrderBy(candidate => candidate.EntityId))
        {
            if (existingById.TryGetValue(region.EntityId, out var existing))
            {
                existing.RefreshFromModel();
                continue;
            }

            var insertAt = Regions.Count;
            for (var i = 0; i < Regions.Count; i++)
            {
                if (Regions[i].Region.EntityId > region.EntityId)
                {
                    insertAt = i;
                    break;
                }
            }

            Regions.Insert(insertAt, new RegionItemViewModel(this, _session, region));
        }

        OnPropertyChanged(nameof(HasRegions));
        OnPropertyChanged(nameof(ShowEmptyMessage));
        OnPropertyChanged(nameof(EmptyMessage));
        TreeRevision++;
        RefreshHighlightStates();
    }

    private void ApplyTreeFocusFromSession()
    {
        if (_session.FocusedRegionEntityId is not int regionId)
        {
            return;
        }

        var item = Regions.FirstOrDefault(candidate => candidate.Region.EntityId == regionId);
        if (item is null)
        {
            return;
        }

        FocusTreeItem(item);
        TreeRevision++;
    }

    private void RefreshHighlightStates()
    {
        foreach (var region in Regions)
        {
            region.RefreshHighlightState();
        }
    }
}

public partial class RegionItemViewModel : ObservableObject
{
    private readonly RegionsToolViewModel _tree;
    private readonly EditorSession _session;
    private bool _suppressTypeBinding;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _renameText = string.Empty;

    public RegionItemViewModel(RegionsToolViewModel tree, EditorSession session, Region region)
    {
        _tree = tree;
        _session = session;
        Region = region;
    }

    public Region Region { get; }

    public IReadOnlyList<RegionType> RegionTypeOptions { get; } = Enum.GetValues<RegionType>();

    public string DisplayName => Region.DisplayName;

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

    public void CancelRename() => IsRenaming = false;

    public bool IsSelected => _session.SelectedRegionEntityIds.Contains(Region.EntityId);

    public bool IsHovered => _session.HoveredRegionEntityId == Region.EntityId;

    public bool IsTreeHighlighted => IsSelected || IsHovered;

    public bool IsActive
    {
        get => Region.IsActive;
        set
        {
            if (Region.IsActive == value)
            {
                return;
            }

            _session.Execute(value ? "Enable region" : "Disable region", () =>
            {
                Region.IsActive = value;
            });
            OnPropertyChanged();
        }
    }

    public RegionType? RegionType
    {
        get => Region.RegionType;
        set
        {
            if (_suppressTypeBinding || value is null || Region.RegionType == value)
            {
                return;
            }

            var selectedIds = _session.SelectedRegionEntityIds.ToHashSet();
            var targets = _session.Map is not null && selectedIds.Contains(Region.EntityId)
                ? _session.Map.Regions.Where(region => selectedIds.Contains(region.EntityId)).ToList()
                : [Region];

            _session.Execute(
                targets.Count > 1 ? "Change region types" : "Change region type",
                () =>
                {
                    foreach (var region in targets)
                    {
                        RegionEditing.SetRegionType(region, value.Value);
                    }
                });
            OnPropertyChanged();
        }
    }

    public void RefreshFromModel()
    {
        _suppressTypeBinding = true;
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(RegionType));
        OnPropertyChanged(nameof(DisplayName));
        RefreshHighlightState();
        Dispatcher.UIThread.Post(
            () => _suppressTypeBinding = false,
            DispatcherPriority.Input);
    }

    public void RefreshHighlightState()
    {
        OnPropertyChanged(nameof(IsSelected));
        OnPropertyChanged(nameof(IsHovered));
        OnPropertyChanged(nameof(IsTreeHighlighted));
    }
}
