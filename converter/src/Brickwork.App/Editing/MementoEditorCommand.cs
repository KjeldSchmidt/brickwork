using Brickwork.Core.Editing;

namespace Brickwork.App.Editing;

public sealed class MementoEditorCommand : IEditorCommand
{
    private readonly DocumentContentMemento _beforeDocument;
    private readonly DocumentContentMemento _afterDocument;

    public MementoEditorCommand(
        string name,
        DocumentContentMemento beforeDocument,
        DocumentContentMemento afterDocument)
    {
        Name = name;
        _beforeDocument = beforeDocument;
        _afterDocument = afterDocument;
    }

    public string Name { get; }

    public void Undo(EditorSession session) => session.RestoreContent(_beforeDocument);

    public void Redo(EditorSession session) => session.RestoreContent(_afterDocument);
}
