using Brickwork.Core.Editing;

namespace Brickwork.App.Editing;

public sealed class MementoEditorCommand : IEditorCommand
{
    private readonly DocumentContentMemento _beforeDocument;
    private readonly DocumentContentMemento _afterDocument;
    private readonly double _beforeTolerance;
    private readonly double _afterTolerance;

    public MementoEditorCommand(
        string name,
        DocumentContentMemento beforeDocument,
        DocumentContentMemento afterDocument,
        double beforeTolerance,
        double afterTolerance)
    {
        Name = name;
        _beforeDocument = beforeDocument;
        _afterDocument = afterDocument;
        _beforeTolerance = beforeTolerance;
        _afterTolerance = afterTolerance;
    }

    public string Name { get; }

    public void Undo(EditorSession session) =>
        session.RestoreContent(_beforeDocument, _beforeTolerance);

    public void Redo(EditorSession session) =>
        session.RestoreContent(_afterDocument, _afterTolerance);
}
