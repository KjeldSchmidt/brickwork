namespace Brickwork.App.Editing;

public interface IEditorCommand
{
    string Name { get; }

    void Undo(EditorSession session);

    void Redo(EditorSession session);
}
