using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using IconPacks.Avalonia.FontAwesome6;

namespace Brickwork.App.ViewModels;

public partial class ToolPaletteViewModel : Tool
{
    private readonly EditorSession _session;

    public ToolPaletteViewModel(EditorSession session)
    {
        _session = session;
        Tools =
        [
            new MapToolItemViewModel(
                MapToolKind.WallEditing,
                "Wall Editing",
                "V",
                PackIconFontAwesome6Kind.DungeonSolid,
                buttonHints:
                [
                    new("Click Wall", "Select Wall"),
                    new("Double-Click Wall", "Change Wall Type"),
                    new("Shift-Click", "Add or Remove Wall"),
                    new("Drag Empty", "Marquee Select"),
                    new("Drag Node", "Move Node, Resize Gap"),
                    new("Drag Wall", "Move Wall (or Selection)"),
                    new("Right-Click Wall", "Add Node"),
                    new("Right-Click Node", "Remove Node or Portal"),
                    new("Double-Click Empty", "Start Drawing Wall"),
                    new("Click", "Add Wall Node (While Drawing)"),
                    new("Double-Click / Enter", "Finish Wall (While Drawing)"),
                    new("Middle-Click Wall", "Add Portal"),
                    new("Middle-Drag Wall", "Add Portal and Resize"),
                    new("Ctrl+C / X / V", "Copy / Cut / Paste"),
                    new("Delete", "Delete Selection"),
                    new("Right-Drag", "Pan Map"),
                ]),
            new MapToolItemViewModel(
                MapToolKind.RegionEditing,
                "Region Editing",
                "R",
                PackIconFontAwesome6Kind.HillRockslideSolid,
                buttonHints:
                [
                    new("Click Region", "Change Region Type"),
                    new("Shift-Click", "Add or Remove Region"),
                    new("Drag Empty", "Marquee Select"),
                    new("Drag Node", "Move Node"),
                    new("Drag Region", "Move Region (or Selection)"),
                    new("Right-Click Region", "Add Node"),
                    new("Right-Click Node", "Remove Node"),
                    new("Double-Click Empty", "Start Drawing Region"),
                    new("Click", "Add Region Node (While Drawing)"),
                    new("Double-Click / Enter", "Finish Region (While Drawing)"),
                    new("Ctrl+C / X / V", "Copy / Cut / Paste"),
                    new("Delete", "Delete Selection"),
                    new("Right-Drag", "Pan Map"),
                ]),
            new MapToolItemViewModel(
                MapToolKind.Eraser,
                "Eraser",
                "E",
                PackIconFontAwesome6Kind.EraserSolid,
                buttonHints:
                [
                    new("Click Wall or Region", "Delete"),
                    new("Right-Drag", "Pan Map"),
                ]),
        ];

        SyncSelectionFromSession();
        SyncHistoryFromSession();
        _session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(EditorSession.ActiveMapTool))
            {
                SyncSelectionFromSession();
            }

            if (args.PropertyName is nameof(EditorSession.CanUndo)
                or nameof(EditorSession.CanRedo)
                or nameof(EditorSession.UndoActionName)
                or nameof(EditorSession.RedoActionName))
            {
                SyncHistoryFromSession();
            }
        };
    }

    public ObservableCollection<MapToolItemViewModel> Tools { get; }

    public PackIconFontAwesome6Kind UndoIconKind => PackIconFontAwesome6Kind.ArrowRotateLeftSolid;

    public PackIconFontAwesome6Kind RedoIconKind => PackIconFontAwesome6Kind.ArrowRotateRightSolid;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    private bool _canUndo;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RedoCommand))]
    private bool _canRedo;

    [ObservableProperty]
    private string _undoToolTip = "Undo";

    [ObservableProperty]
    private string _redoToolTip = "Redo";

    [RelayCommand]
    private void SelectTool(MapToolKind kind)
    {
        _session.ActiveMapTool = kind;
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => _session.Undo();

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => _session.Redo();

    private void SyncSelectionFromSession()
    {
        foreach (var tool in Tools)
        {
            tool.IsSelected = tool.Kind == _session.ActiveMapTool;
        }
    }

    private void SyncHistoryFromSession()
    {
        CanUndo = _session.CanUndo;
        CanRedo = _session.CanRedo;
        UndoToolTip = string.IsNullOrWhiteSpace(_session.UndoActionName)
            ? "Undo (Ctrl+Z)"
            : $"Undo {_session.UndoActionName} (Ctrl+Z)";
        RedoToolTip = string.IsNullOrWhiteSpace(_session.RedoActionName)
            ? "Redo (Ctrl+Y)"
            : $"Redo {_session.RedoActionName} (Ctrl+Y)";
    }
}
