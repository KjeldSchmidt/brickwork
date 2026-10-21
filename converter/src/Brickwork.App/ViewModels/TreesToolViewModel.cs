using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Dock.Model.Mvvm.Controls;
using Brickwork.App.Rendering;
using Brickwork.Core.Models;

namespace Brickwork.App.ViewModels;

/// <summary>Lists the tree trunk walls (see <see cref="TreeTrunks"/>), grouped by tree kind.</summary>
public partial class TreesToolViewModel : Tool
{
    private readonly EditorSession _session;
    private bool _syncingSelection;

    [ObservableProperty]
    private ObservableCollection<TreeKindViewModel> _kinds = [];

    [ObservableProperty]
    private object? _selectedTreeItem;

    public TreesToolViewModel(EditorSession session)
    {
        _session = session;
        _session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(EditorSession.Map))
            {
                Rebuild();
            }

            if (args.PropertyName is nameof(EditorSession.ContentRevision) && !_session.IsGestureActive)
            {
                Sync();
            }

            if (args.PropertyName is nameof(EditorSession.TreeFocusGeneration))
            {
                SelectVersionOfFocusedTrunk();
            }

            if (args.PropertyName is nameof(EditorSession.FocusedWallEntityId) &&
                _session.FocusedWallEntityId is null &&
                SelectedTreeItem is TreeItemViewModel)
            {
                _syncingSelection = true;
                SelectedTreeItem = null;
                _syncingSelection = false;
            }
        };
        Rebuild();
    }

    public bool HasTrees => Kinds.Count > 0;

    public bool ShowEmptyMessage => !HasTrees;

    public string EmptyMessage => _session.Map is null
        ? "Open a Source Map to see trees"
        : "No trees found. Stamps whose names contain a tree word (tree, palm, oak, …) get a small X of walls at their trunk; the words can be changed under Settings → Import.";

    public string Summary
    {
        get
        {
            var trees = Kinds.SelectMany(kind => kind.AllTrees).ToList();
            return $"{trees.Count(tree => tree.IsActive)} of {trees.Count} tree trunks exported as walls.";
        }
    }

    partial void OnSelectedTreeItemChanged(object? value)
    {
        if (_syncingSelection)
        {
            return;
        }

        if (value is TreeItemViewModel tree)
        {
            _session.SetSelection(tree.Walls.Select(wall => wall.EntityId), tree.Walls[0].EntityId);
        }
        else if (value is TreeGroupViewModel group)
        {
            _session.SetSelection(group.Walls.Select(wall => wall.EntityId));
        }
    }

    internal void NotifySummaryChanged() => OnPropertyChanged(nameof(Summary));

    private bool IsShownTab => _session.IsTreesPanelShown;

    /// <summary>
    /// While this tab is shown, a trunk clicked on the map selects its tree version here (the kind
    /// when it has only one version), ready for scale and offset. On the map only the clicked tree's
    /// X stays selected, so a drag moves that tree and not the whole version.
    /// </summary>
    private void SelectVersionOfFocusedTrunk()
    {
        if (!IsShownTab ||
            _session.FocusedWallEntityId is not int wallId ||
            _session.Map?.Walls.FirstOrDefault(wall => wall.EntityId == wallId) is not { Trunk: not null } focused)
        {
            return;
        }

        foreach (var kind in Kinds)
        {
            var version = kind.Versions.FirstOrDefault(candidate => candidate.Walls.Contains(focused));
            if (version is null)
            {
                continue;
            }

            kind.IsExpanded = true;
            var tree = version.Trees.FirstOrDefault(candidate => candidate.Walls.Contains(focused));
            object target = kind.Versions.Count == 1 ? kind : version;

            // Select after the expanded rows exist; don't let the selection grab the whole version.
            Dispatcher.UIThread.Post(() =>
            {
                _syncingSelection = true;
                SelectedTreeItem = target;
                _syncingSelection = false;
            });

            // Safe during a drag too: the walls being dragged were fixed when it started.
            if (tree is not null)
            {
                _session.SetSelection(tree.Walls.Select(wall => wall.EntityId), focused.EntityId);
            }

            return;
        }
    }

    private void Rebuild()
    {
        _syncingSelection = true;
        SelectedTreeItem = null;
        _syncingSelection = false;

        var expanded = Kinds.Where(kind => kind.IsExpanded).Select(kind => kind.Name).ToHashSet();
        var kinds = new ObservableCollection<TreeKindViewModel>();
        if (_session.Map is { } map)
        {
            var thumbnailSize = map.Grid.CellSize > 0 ? map.Grid.CellSize * 2.5 : 0;
            foreach (var kind in map.Walls
                         .Where(TreeTrunks.IsTrunk)
                         .GroupBy(wall => wall.DisplayName, StringComparer.OrdinalIgnoreCase)
                         .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
            {
                var versions = kind
                    .GroupBy(wall => wall.Trunk!.AssetId)
                    .OrderBy(group => group.Key)
                    .Select((version, index) =>
                    {
                        var trees = version
                            .GroupBy(wall => wall.Trunk!.StampId)
                            .OrderBy(group => group.Key)
                            .Select((walls, treeIndex) => new TreeItemViewModel(
                                this,
                                _session,
                                walls.OrderByDescending(wall => wall.EntityId).ToList(),
                                treeIndex + 1))
                            .ToList();
                        var thumbnail = thumbnailSize > 0
                            ? MapPreviewThumbnails.Create(map, trees[0].Center, thumbnailSize)
                            : null;
                        return new TreeVersionViewModel(this, _session, index + 1, trees, thumbnail);
                    })
                    .ToList();
                kinds.Add(new TreeKindViewModel(this, _session, kind.Key, versions)
                {
                    IsExpanded = expanded.Contains(kind.Key),
                });
            }
        }

        Kinds = kinds;
        OnPropertyChanged(nameof(HasTrees));
        OnPropertyChanged(nameof(ShowEmptyMessage));
        OnPropertyChanged(nameof(EmptyMessage));
        OnPropertyChanged(nameof(Summary));
    }

    /// <summary>Refreshes values in place, or rebuilds when trunk walls were added or removed.</summary>
    private void Sync()
    {
        var map = _session.Map;
        var shown = Kinds.SelectMany(kind => kind.Walls).ToList();
        var current = map?.Walls.Where(TreeTrunks.IsTrunk).ToList() ?? [];
        if (shown.Count != current.Count || shown.Any(wall => !current.Contains(wall)))
        {
            Rebuild();
            return;
        }

        foreach (var kind in Kinds)
        {
            kind.RefreshFromModel();
        }

        OnPropertyChanged(nameof(Summary));
    }
}

/// <summary>A set of tree trunks (a tree kind, or one version of it) switched and scaled together.</summary>
public abstract partial class TreeGroupViewModel : ObservableObject
{
    private readonly TreesToolViewModel _tool;
    private readonly EditorSession _session;

    [ObservableProperty]
    private bool _isExpanded;

    protected TreeGroupViewModel(TreesToolViewModel tool, EditorSession session, IReadOnlyList<Wall> walls)
    {
        _tool = tool;
        _session = session;
        Walls = walls;
    }

    public IReadOnlyList<Wall> Walls { get; }

    public abstract string Header { get; }

    public abstract IEnumerable<object> ChildNodes { get; }

    public bool? IsActive
    {
        get => Walls.All(IsWallOn) ? true : Walls.Any(IsWallOn) ? null : false;
        set
        {
            // A click on an indeterminate box reaches here as null; treat it as "enable all".
            var enabled = value ?? true;
            Apply(
                enabled ? "Enable tree trunks" : "Disable tree trunks",
                wall => WallLineEditing.SetWallEnabled(wall, enabled));
        }
    }

    /// <summary>The versions this row adjusts (a kind adjusts all of its versions).</summary>
    public abstract IReadOnlyList<TreeVersionViewModel> AdjustedVersions { get; }

    /// <summary>Trunk scale factor (1 = size from the import settings); null when the trunks differ.</summary>
    public decimal? Scale
    {
        get => Common(trunk => trunk.Scale);
        set
        {
            if (value is decimal scale && scale > 0 && scale != Scale)
            {
                Adjust("Scale tree trunks", trunk => (Scale: (double)scale, trunk.OffsetX, trunk.OffsetY));
            }
        }
    }

    /// <summary>Trunk offset to the right, in grid squares in the tree image's frame.</summary>
    public decimal? OffsetX
    {
        get => Common(trunk => trunk.OffsetX);
        set
        {
            if (value is decimal offset && offset != OffsetX)
            {
                Adjust("Move tree trunks", trunk => (trunk.Scale, OffsetX: (double)offset, trunk.OffsetY));
            }
        }
    }

    /// <summary>Trunk offset downwards, in grid squares in the tree image's frame.</summary>
    public decimal? OffsetY
    {
        get => Common(trunk => trunk.OffsetY);
        set
        {
            if (value is decimal offset && offset != OffsetY)
            {
                Adjust("Move tree trunks", trunk => (trunk.Scale, trunk.OffsetX, OffsetY: (double)offset));
            }
        }
    }

    /// <summary>Back to the built-in value for these versions (scale 1, no offset when there is none).</summary>
    [RelayCommand]
    private void ResetAdjustment()
    {
        if (_session.Map is not { } map)
        {
            return;
        }

        _session.Execute(
            "Reset tree trunks",
            () => TreeTrunkEditing.ResetVersions(_session, map, AdjustedVersions.Select(version => version.Key)));
        RefreshFromModel();
        _tool.NotifySummaryChanged();
    }

    private decimal? Common(Func<TreeTrunk, double> selector)
    {
        var values = Walls
            .Where(wall => wall.Trunk is not null)
            .Select(wall => Math.Round(selector(wall.Trunk!), 3))
            .Distinct()
            .ToList();
        return values.Count == 1 ? (decimal)values[0] : null;
    }

    /// <summary>
    /// Changes scale/offset of these trunks in the map (one undo step) and remembers the new values
    /// per version in the tree-trunk library, so other maps using the same tree image get them too.
    /// </summary>
    private void Adjust(string name, Func<TreeTrunk, (double Scale, double OffsetX, double OffsetY)> change)
    {
        if (_session.Map is not { } map)
        {
            return;
        }

        _session.Execute(
            name,
            () => TreeTrunkEditing.AdjustVersions(_session, map, AdjustedVersions.Select(version => version.Key), change));
        RefreshFromModel();
        _tool.NotifySummaryChanged();
    }

    public virtual void RefreshFromModel()
    {
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(Scale));
        OnPropertyChanged(nameof(OffsetX));
        OnPropertyChanged(nameof(OffsetY));
        foreach (var child in ChildNodes)
        {
            switch (child)
            {
                case TreeGroupViewModel group:
                    group.RefreshFromModel();
                    break;
                case TreeItemViewModel tree:
                    tree.RefreshFromModel();
                    break;
            }
        }
    }

    internal static bool IsWallOn(Wall wall) => wall.IsActive && wall.LineType != WallLineType.Disabled;

    private void Apply(string name, Action<Wall> mutate)
    {
        _session.Execute(name, () =>
        {
            foreach (var wall in Walls)
            {
                mutate(wall);
            }
        });
        RefreshFromModel();
        _tool.NotifySummaryChanged();
    }
}

public sealed partial class TreeKindViewModel : TreeGroupViewModel
{
    public TreeKindViewModel(
        TreesToolViewModel tool,
        EditorSession session,
        string name,
        IReadOnlyList<TreeVersionViewModel> versions)
        : base(tool, session, versions.SelectMany(version => version.Walls).ToList())
    {
        Name = name;
        Versions = versions;

        // Only show the version level when there is more than one version.
        Children = versions.Count == 1
            ? new ObservableCollection<object>(versions[0].Trees)
            : new ObservableCollection<object>(versions);
    }

    public string Name { get; }

    public IReadOnlyList<TreeVersionViewModel> Versions { get; }

    public ObservableCollection<object> Children { get; }

    public int TreeCount => Versions.Sum(version => version.Trees.Count);

    public override string Header => Versions.Count == 1
        ? $"{Name} ({TreeCount})"
        : $"{Name} ({TreeCount}, {Versions.Count} versions)";

    public override IEnumerable<object> ChildNodes => Versions.Count == 1 ? Versions[0].Trees : Versions;

    public override IReadOnlyList<TreeVersionViewModel> AdjustedVersions => Versions;

    /// <summary>Offsets are per tree image, so they are only editable here when there is one version.</summary>
    public bool HasSingleVersion => Versions.Count == 1;

    public IEnumerable<TreeItemViewModel> AllTrees => Versions.SelectMany(version => version.Trees);

    public override void RefreshFromModel()
    {
        base.RefreshFromModel();
        if (Versions.Count == 1)
        {
            // The single version is not shown, but its scale backs this row.
            Versions[0].RefreshFromModel();
        }
    }
}

/// <summary>Trees of one kind that use the same Inkarnate asset (e.g. one of five palm images).</summary>
public sealed partial class TreeVersionViewModel : TreeGroupViewModel
{
    public TreeVersionViewModel(
        TreesToolViewModel tool,
        EditorSession session,
        int number,
        IReadOnlyList<TreeItemViewModel> trees,
        Bitmap? thumbnail)
        : base(tool, session, trees.SelectMany(tree => tree.Walls).ToList())
    {
        Number = number;
        Trees = trees;
        Children = new ObservableCollection<TreeItemViewModel>(trees);
        Thumbnail = thumbnail;
    }

    public int Number { get; }

    public string KindName => Walls[0].DisplayName;

    /// <summary>Key in the tree-trunk library (the Inkarnate image id).</summary>
    public string Key => TreeTrunks.VersionKey(Walls[0]);

    public override IReadOnlyList<TreeVersionViewModel> AdjustedVersions => [this];

    public IReadOnlyList<TreeItemViewModel> Trees { get; }

    public ObservableCollection<TreeItemViewModel> Children { get; }

    public Bitmap? Thumbnail { get; }

    public override string Header => $"Version {Number} ({Trees.Count})";

    public override IEnumerable<object> ChildNodes => Trees;
}

public partial class TreeItemViewModel : ObservableObject
{
    private readonly TreesToolViewModel _tool;
    private readonly EditorSession _session;

    public TreeItemViewModel(TreesToolViewModel tool, EditorSession session, IReadOnlyList<Wall> walls, int number)
    {
        _tool = tool;
        _session = session;
        Walls = walls;
        Number = number;
    }

    /// <summary>The (usually two) walls of this tree's trunk X.</summary>
    public IReadOnlyList<Wall> Walls { get; }

    public int Number { get; }

    public string DisplayName => $"#{Number}";

    public MapPoint Center
    {
        get
        {
            var points = Walls.SelectMany(wall => wall.Points).ToList();
            return points.Count == 0
                ? default
                : new MapPoint(points.Average(point => point.X), points.Average(point => point.Y));
        }
    }

    public string PositionText => string.Create(CultureInfo.InvariantCulture, $"({Center.X:0}, {Center.Y:0})");

    public bool IsActive
    {
        get => Walls.All(TreeGroupViewModel.IsWallOn);
        set
        {
            if (IsActive == value)
            {
                return;
            }

            _session.Execute(
                value ? "Enable tree trunk" : "Disable tree trunk",
                () =>
                {
                    foreach (var wall in Walls)
                    {
                        WallLineEditing.SetWallEnabled(wall, value);
                    }
                });
            OnPropertyChanged();
            _tool.NotifySummaryChanged();
        }
    }

    public void RefreshFromModel()
    {
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(PositionText));
    }
}
