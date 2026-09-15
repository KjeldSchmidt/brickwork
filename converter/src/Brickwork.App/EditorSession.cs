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
    private int _highlightRevision;

    [ObservableProperty]
    private double _wallSimplificationTolerance = WallSimplificationSettings.DefaultToleranceSceneUnits;

    [ObservableProperty]
    private MapToolKind _activeMapTool = MapToolKind.Pointer;

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    [ObservableProperty]
    private string? _undoActionName;

    [ObservableProperty]
    private string? _redoActionName;

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

    public void SetFocusedWall(Wall wall, WallPortal? portal = null)
    {
        FocusedWallEntityId = wall.EntityId;
        FocusedPortal = portal;
        HighlightRevision++;
    }

    public void RequestWallTreeFocus(Wall wall, WallPortal? portal = null)
    {
        SetFocusedWall(wall, portal);
        TreeFocusGeneration++;
    }

    public void SetHoveredWall(Wall? wall, WallPortal? portal = null)
    {
        var entityId = wall?.EntityId;
        if (HoveredWallEntityId == entityId && ReferenceEquals(HoveredPortal, portal))
        {
            return;
        }

        HoveredWallEntityId = entityId;
        HoveredPortal = portal;
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

    public void ClearWallSelection()
    {
        if (FocusedWallEntityId is null &&
            FocusedPortal is null &&
            HoveredWallEntityId is null &&
            HoveredPortal is null)
        {
            return;
        }

        FocusedWallEntityId = null;
        FocusedPortal = null;
        HoveredWallEntityId = null;
        HoveredPortal = null;
        HighlightRevision++;
    }

    partial void OnMapChanged(MapDocument? value)
    {
        _activeGesture?.Cancel();
        _activeGesture = null;
        _history.Clear();
        RefreshHistoryState();
        ClearWallSelection();
    }

    private void CompleteGesture(EditGesture gesture)
    {
        if (!ReferenceEquals(_activeGesture, gesture))
        {
            return;
        }

        _activeGesture = null;
        if (Map is null || gesture.Cancelled)
        {
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
