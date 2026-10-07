using CommunityToolkit.Mvvm.ComponentModel;
using Brickwork.App.Editing;
using Brickwork.Core.Editing;
using Brickwork.Core.Geometry;
using Brickwork.Core.Models;

namespace Brickwork.App;

public sealed partial class EditorSession : ObservableObject
{
    private readonly EditorHistory _history = new();
    private EditGesture? _activeGesture;
    private bool _restoringHistory;
    private readonly HashSet<int> _selectedWallEntityIds = [];
    private readonly HashSet<int> _selectedRegionEntityIds = [];
    private MapEntityClipboard _clipboard = MapEntityClipboard.Empty;
    private int _pasteCount;

    [ObservableProperty]
    private MapDocument? _map;

    [ObservableProperty]
    private string? _sourceFileName;

    [ObservableProperty]
    private string? _sourceFilePath;

    [ObservableProperty]
    private int _contentRevision;

    [ObservableProperty]
    private int _treeFocusGeneration;

    [ObservableProperty]
    private int? _focusedWallEntityId;

    [ObservableProperty]
    private WallPortal? _focusedPortal;

    [ObservableProperty]
    private int? _hoveredWallEntityId;

    [ObservableProperty]
    private WallPortal? _hoveredPortal;

    [ObservableProperty]
    private int? _focusedRegionEntityId;

    [ObservableProperty]
    private int? _hoveredRegionEntityId;

    [ObservableProperty]
    private int _highlightRevision;

    [ObservableProperty]
    private double _wallSimplificationTolerance = WallSimplificationSettings.DefaultToleranceSceneUnits;

    [ObservableProperty]
    private MapToolKind _activeMapTool = MapToolKind.WallEditing;

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    [ObservableProperty]
    private string? _undoActionName;

    [ObservableProperty]
    private string? _redoActionName;

    public IReadOnlySet<int> SelectedWallEntityIds => _selectedWallEntityIds;

    public IReadOnlySet<int> SelectedRegionEntityIds => _selectedRegionEntityIds;

    public bool CanPaste => !_clipboard.IsEmpty;

    /// <summary>Scene-space nudge applied on each paste (stacks for repeated pastes).</summary>
    public const double PasteOffsetSceneUnits = 40d;

    public void NotifyContentChanged()
    {
        ContentRevision++;
    }

    public void Execute(string name, Action mutate)
    {
        if (Map is null)
        {
            mutate();
            return;
        }

        if (_activeGesture is not null || _restoringHistory)
        {
            mutate();
            NotifyContentChanged();
            return;
        }

        var beforeDocument = DocumentContentMemento.Capture(Map);
        mutate();
        var afterDocument = DocumentContentMemento.Capture(Map);

        if (beforeDocument.ContentEquals(afterDocument))
        {
            return;
        }

        _history.Push(new MementoEditorCommand(name, beforeDocument, afterDocument));
        RefreshHistoryState();
        NotifyContentChanged();
    }

    public IDisposable BeginGesture(string name)
    {
        if (Map is null || _activeGesture is not null || _restoringHistory)
        {
            return EmptyDisposable.Instance;
        }

        _activeGesture = new EditGesture(this, name, DocumentContentMemento.Capture(Map));
        return _activeGesture;
    }

    public void Undo()
    {
        if (Map is null || _activeGesture is not null)
        {
            return;
        }

        var command = _history.PopUndo();
        if (command is null)
        {
            return;
        }

        command.Undo(this);
        RefreshHistoryState();
        ClearWallSelection();
        ClearRegionSelection();
        NotifyContentChanged();
    }

    public void Redo()
    {
        if (Map is null || _activeGesture is not null)
        {
            return;
        }

        var command = _history.PopRedo();
        if (command is null)
        {
            return;
        }

        command.Redo(this);
        RefreshHistoryState();
        ClearWallSelection();
        ClearRegionSelection();
        NotifyContentChanged();
    }

    internal void RestoreContent(DocumentContentMemento document)
    {
        if (Map is null)
        {
            return;
        }

        _restoringHistory = true;
        try
        {
            document.RestoreTo(Map);
        }
        finally
        {
            _restoringHistory = false;
        }
    }

    public void SetSelection(
        IEnumerable<int> wallIds,
        int? primaryId = null,
        WallPortal? portal = null)
    {
        ClearRegionSelection();

        var next = wallIds.ToHashSet();
        int? primary = primaryId;
        if (next.Count == 0)
        {
            primary = null;
            portal = null;
        }
        else
        {
            primary ??= next.OrderBy(id => id).First();
            if (primary is int id && !next.Contains(id))
            {
                primary = next.OrderBy(candidate => candidate).First();
                portal = null;
            }
        }

        var selectionChanged = !_selectedWallEntityIds.SetEquals(next);
        var focusChanged = FocusedWallEntityId != primary || !ReferenceEquals(FocusedPortal, portal);

        if (!selectionChanged && !focusChanged)
        {
            return;
        }

        if (selectionChanged)
        {
            _selectedWallEntityIds.Clear();
            foreach (var id in next)
            {
                _selectedWallEntityIds.Add(id);
            }
        }

        FocusedWallEntityId = primary;
        FocusedPortal = portal;
        HighlightRevision++;
    }

    public void SetRegionSelection(IEnumerable<int> regionIds, int? primaryId = null)
    {
        ClearWallSelection();

        var next = regionIds.ToHashSet();
        int? primary = primaryId;
        if (next.Count == 0)
        {
            primary = null;
        }
        else
        {
            primary ??= next.OrderBy(id => id).First();
            if (primary is int id && !next.Contains(id))
            {
                primary = next.OrderBy(candidate => candidate).First();
            }
        }

        var selectionChanged = !_selectedRegionEntityIds.SetEquals(next);
        var focusChanged = FocusedRegionEntityId != primary;
        if (!selectionChanged && !focusChanged)
        {
            return;
        }

        if (selectionChanged)
        {
            _selectedRegionEntityIds.Clear();
            foreach (var id in next)
            {
                _selectedRegionEntityIds.Add(id);
            }
        }

        FocusedRegionEntityId = primary;
        HighlightRevision++;
    }

    public void ToggleWallInSelection(int wallId)
    {
        ClearRegionSelection();

        if (_selectedWallEntityIds.Contains(wallId))
        {
            _selectedWallEntityIds.Remove(wallId);
            if (FocusedWallEntityId == wallId)
            {
                FocusedWallEntityId = _selectedWallEntityIds.OrderBy(id => id).Cast<int?>().FirstOrDefault();
                FocusedPortal = null;
            }
        }
        else
        {
            _selectedWallEntityIds.Add(wallId);
            FocusedWallEntityId = wallId;
            FocusedPortal = null;
        }

        HighlightRevision++;
    }

    public void ToggleRegionInSelection(int regionId)
    {
        ClearWallSelection();

        if (_selectedRegionEntityIds.Contains(regionId))
        {
            _selectedRegionEntityIds.Remove(regionId);
            if (FocusedRegionEntityId == regionId)
            {
                FocusedRegionEntityId = _selectedRegionEntityIds.OrderBy(id => id).Cast<int?>().FirstOrDefault();
            }
        }
        else
        {
            _selectedRegionEntityIds.Add(regionId);
            FocusedRegionEntityId = regionId;
        }

        HighlightRevision++;
    }

    public void AddWallsToSelection(IEnumerable<int> wallIds)
    {
        ClearRegionSelection();

        var added = false;
        foreach (var id in wallIds)
        {
            added |= _selectedWallEntityIds.Add(id);
        }

        if (!added)
        {
            return;
        }

        FocusedWallEntityId ??= _selectedWallEntityIds.OrderBy(id => id).First();
        HighlightRevision++;
    }

    public void AddRegionsToSelection(IEnumerable<int> regionIds)
    {
        ClearWallSelection();

        var added = false;
        foreach (var id in regionIds)
        {
            added |= _selectedRegionEntityIds.Add(id);
        }

        if (!added)
        {
            return;
        }

        FocusedRegionEntityId ??= _selectedRegionEntityIds.OrderBy(id => id).First();
        HighlightRevision++;
    }

    public void RemoveWallsFromSelection(IEnumerable<int> wallIds)
    {
        var removed = false;
        foreach (var id in wallIds)
        {
            removed |= _selectedWallEntityIds.Remove(id);
        }

        if (!removed)
        {
            return;
        }

        if (FocusedWallEntityId is int focusedId && !_selectedWallEntityIds.Contains(focusedId))
        {
            FocusedWallEntityId = _selectedWallEntityIds.OrderBy(id => id).Cast<int?>().FirstOrDefault();
            FocusedPortal = null;
        }

        HighlightRevision++;
    }

    public void SetFocusedWall(Wall wall, WallPortal? portal = null)
    {
        SetSelection([wall.EntityId], wall.EntityId, portal);
    }

    public void SetFocusedRegion(Region region) =>
        SetRegionSelection([region.EntityId], region.EntityId);

    /// <summary>
    /// Updates focus/hover primary without changing the wall multi-selection set.
    /// Used when multi-selecting portals in the tree.
    /// </summary>
    public void SetPrimaryFocus(int wallEntityId, WallPortal? portal = null)
    {
        if (FocusedWallEntityId == wallEntityId && ReferenceEquals(FocusedPortal, portal))
        {
            return;
        }

        FocusedWallEntityId = wallEntityId;
        FocusedPortal = portal;
        HighlightRevision++;
    }

    public void RequestWallTreeFocus(Wall wall, WallPortal? portal = null)
    {
        SetFocusedWall(wall, portal);
        TreeFocusGeneration++;
    }

    public void RequestRegionTreeFocus(Region region)
    {
        SetFocusedRegion(region);
        TreeFocusGeneration++;
    }

    public void SetHoveredWall(Wall? wall, WallPortal? portal = null)
    {
        var entityId = wall?.EntityId;
        if (HoveredWallEntityId == entityId && ReferenceEquals(HoveredPortal, portal))
        {
            return;
        }

        HoveredRegionEntityId = null;
        HoveredWallEntityId = entityId;
        HoveredPortal = portal;
        HighlightRevision++;
    }

    public void SetHoveredRegion(Region? region)
    {
        var entityId = region?.EntityId;
        if (HoveredRegionEntityId == entityId && HoveredWallEntityId is null && HoveredPortal is null)
        {
            return;
        }

        HoveredWallEntityId = null;
        HoveredPortal = null;
        HoveredRegionEntityId = entityId;
        HighlightRevision++;
    }

    public void ClearHoveredWall()
    {
        if (HoveredWallEntityId is null && HoveredPortal is null)
        {
            return;
        }

        HoveredWallEntityId = null;
        HoveredPortal = null;
        HighlightRevision++;
    }

    public void ClearHoveredRegion()
    {
        if (HoveredRegionEntityId is null)
        {
            return;
        }

        HoveredRegionEntityId = null;
        HighlightRevision++;
    }

    public void ClearHover()
    {
        if (HoveredWallEntityId is null &&
            HoveredPortal is null &&
            HoveredRegionEntityId is null)
        {
            return;
        }

        HoveredWallEntityId = null;
        HoveredPortal = null;
        HoveredRegionEntityId = null;
        HighlightRevision++;
    }

    public void ClearWallSelection()
    {
        if (_selectedWallEntityIds.Count == 0 &&
            FocusedWallEntityId is null &&
            FocusedPortal is null &&
            HoveredWallEntityId is null &&
            HoveredPortal is null)
        {
            return;
        }

        _selectedWallEntityIds.Clear();
        FocusedWallEntityId = null;
        FocusedPortal = null;
        HoveredWallEntityId = null;
        HoveredPortal = null;
        HighlightRevision++;
    }

    public void ClearRegionSelection()
    {
        if (_selectedRegionEntityIds.Count == 0 &&
            FocusedRegionEntityId is null &&
            HoveredRegionEntityId is null)
        {
            return;
        }

        _selectedRegionEntityIds.Clear();
        FocusedRegionEntityId = null;
        HoveredRegionEntityId = null;
        HighlightRevision++;
    }

    public bool DeleteSelectedWalls()
    {
        if (Map is null || _selectedWallEntityIds.Count == 0)
        {
            return false;
        }

        var ids = _selectedWallEntityIds.ToHashSet();
        Execute("Delete walls", () =>
        {
            foreach (var wall in Map.Walls.Where(candidate => ids.Contains(candidate.EntityId)).ToList())
            {
                WallLineEditing.RemoveFromMap(Map, wall);
            }
        });

        ClearWallSelection();
        return true;
    }

    public bool DeleteSelectedRegions()
    {
        if (Map is null || _selectedRegionEntityIds.Count == 0)
        {
            return false;
        }

        var ids = _selectedRegionEntityIds.ToHashSet();
        Execute("Delete regions", () =>
        {
            foreach (var region in Map.Regions.Where(candidate => ids.Contains(candidate.EntityId)).ToList())
            {
                RegionEditing.RemoveFromMap(Map, region);
            }
        });

        ClearRegionSelection();
        return true;
    }

    public bool DeleteSelection()
    {
        if (_selectedRegionEntityIds.Count > 0)
        {
            return DeleteSelectedRegions();
        }

        return DeleteSelectedWalls();
    }

    public bool CopySelection()
    {
        if (Map is null)
        {
            return false;
        }

        if (_selectedWallEntityIds.Count == 0 && _selectedRegionEntityIds.Count == 0)
        {
            return false;
        }

        _clipboard = MapEntityClipboard.Capture(Map, _selectedWallEntityIds, _selectedRegionEntityIds);
        _pasteCount = 0;
        OnPropertyChanged(nameof(CanPaste));
        return !_clipboard.IsEmpty;
    }

    public bool CutSelection()
    {
        if (!CopySelection())
        {
            return false;
        }

        return DeleteSelection();
    }

    public bool PasteClipboard()
    {
        if (Map is null || _clipboard.IsEmpty)
        {
            return false;
        }

        _pasteCount++;
        var offset = PasteOffsetSceneUnits * _pasteCount;
        IReadOnlyList<Wall> pastedWalls = [];
        IReadOnlyList<Region> pastedRegions = [];

        Execute("Paste", () =>
        {
            (pastedWalls, pastedRegions) = _clipboard.PasteInto(Map, offset, offset);
        });

        if (pastedWalls.Count == 0 && pastedRegions.Count == 0)
        {
            return false;
        }

        if (pastedWalls.Count > 0)
        {
            SetSelection(pastedWalls.Select(wall => wall.EntityId));
            TreeFocusGeneration++;
        }
        else
        {
            SetRegionSelection(pastedRegions.Select(region => region.EntityId));
            TreeFocusGeneration++;
        }

        return true;
    }

    partial void OnMapChanged(MapDocument? value)
    {
        _activeGesture?.Cancel();
        _activeGesture = null;
        _history.Clear();
        RefreshHistoryState();
        ClearWallSelection();
        ClearRegionSelection();
        // Keep clipboard across map loads so paste still works after reopening a file.
        _pasteCount = 0;
    }

    private void CompleteGesture(EditGesture gesture)
    {
        if (!ReferenceEquals(_activeGesture, gesture))
        {
            return;
        }

        _activeGesture = null;
        if (Map is null)
        {
            return;
        }

        if (gesture.Cancelled)
        {
            RestoreContent(gesture.BeforeDocument);
            NotifyContentChanged();
            return;
        }

        var afterDocument = DocumentContentMemento.Capture(Map);
        if (gesture.BeforeDocument.ContentEquals(afterDocument))
        {
            return;
        }

        _history.Push(new MementoEditorCommand(gesture.Name, gesture.BeforeDocument, afterDocument));
        RefreshHistoryState();
        NotifyContentChanged();
    }

    public void CancelActiveGesture()
    {
        if (_activeGesture is null)
        {
            return;
        }

        _activeGesture.Cancel();
        _activeGesture.Dispose();
    }

    private void RefreshHistoryState()
    {
        CanUndo = _history.CanUndo;
        CanRedo = _history.CanRedo;
        UndoActionName = _history.UndoName;
        RedoActionName = _history.RedoName;
    }

    private sealed class EditGesture : IDisposable
    {
        private readonly EditorSession _session;
        private bool _disposed;

        public EditGesture(EditorSession session, string name, DocumentContentMemento beforeDocument)
        {
            _session = session;
            Name = name;
            BeforeDocument = beforeDocument;
        }

        public string Name { get; }

        public DocumentContentMemento BeforeDocument { get; }

        public bool Cancelled { get; private set; }

        public void Cancel() => Cancelled = true;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _session.CompleteGesture(this);
        }
    }

    private sealed class EmptyDisposable : IDisposable
    {
        public static readonly EmptyDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}
