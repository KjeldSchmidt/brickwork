using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace Brickwork.App.ViewModels;

public partial class ToolPaletteViewModel : Tool
{
    // Classic arrow cursor outline (viewBox ~0 0 24 24).
    private const string PointerIconGeometry =
        "M4.5 2.5 L4.5 19.5 L9.2 14.8 L12.8 22.5 L15.2 21.4 L11.5 13.5 L18.5 13.5 Z";

    private readonly EditorSession _session;

    public ToolPaletteViewModel(EditorSession session)
    {
        _session = session;
        Tools =
        [
            new MapToolItemViewModel(
                MapToolKind.Pointer,
                "Select",
                "V",
                PointerIconGeometry,
                buttonHints:
                [
                    new("Click Wall", "Change Wall Type"),
                    new("Drag Node", "Move Node, Resize Gap"),
                    new("Middle-Click", "Toggle Wall Active"),
                    new("Right-Drag", "Pan Map"),
                ]),
        ];

        SyncSelectionFromSession();
        _session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(EditorSession.ActiveMapTool))
            {
                SyncSelectionFromSession();
            }
        };
    }

    public ObservableCollection<MapToolItemViewModel> Tools { get; }

    [RelayCommand]
    private void SelectTool(MapToolKind kind)
    {
        _session.ActiveMapTool = kind;
    }

    private void SyncSelectionFromSession()
    {
        foreach (var tool in Tools)
        {
            tool.IsSelected = tool.Kind == _session.ActiveMapTool;
        }
    }
}
