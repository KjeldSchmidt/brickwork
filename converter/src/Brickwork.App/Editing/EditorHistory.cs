namespace Brickwork.App.Editing;

public sealed class EditorHistory
{
    public const int DefaultMaxDepth = 50;

    private readonly List<IEditorCommand> _undo = [];
    private readonly List<IEditorCommand> _redo = [];

    public EditorHistory(int maxDepth = DefaultMaxDepth)
    {
        MaxDepth = Math.Max(1, maxDepth);
    }

    public int MaxDepth { get; }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public string? UndoName => CanUndo ? _undo[^1].Name : null;

    public string? RedoName => CanRedo ? _redo[^1].Name : null;

    public void Push(IEditorCommand command)
    {
        _undo.Add(command);
        if (_undo.Count > MaxDepth)
        {
            _undo.RemoveAt(0);
        }

        _redo.Clear();
    }

    public IEditorCommand? PopUndo()
    {
        if (_undo.Count == 0)
        {
            return null;
        }

        var command = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(command);
        return command;
    }

    public IEditorCommand? PopRedo()
    {
        if (_redo.Count == 0)
        {
            return null;
        }

        var command = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(command);
        return command;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
