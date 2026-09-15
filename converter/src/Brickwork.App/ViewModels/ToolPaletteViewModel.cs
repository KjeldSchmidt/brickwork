using System.Collections.ObjectModel;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace Brickwork.App.ViewModels;

public partial class ToolPaletteViewModel : Tool
{
    // Classic arrow cursor outline (viewBox ~0 0 24 24).
    private const string PointerIconGeometry =
        "M4.5 2.5 L4.5 19.5 L9.2 14.8 L12.8 22.5 L15.2 21.4 L11.5 13.5 L18.5 13.5 Z";

    // Angled eraser block (viewBox ~0 0 24 24).
    private const string EraserIconGeometry =
        "M16.2 3.2 L20.8 7.8 C21.5 8.5 21.5 9.6 20.8 10.3 L10.3 20.8 C9.9 21.2 9.4 21.4 8.8 21.4 H3.6 V16.2 C3.6 15.6 3.8 15.1 4.2 14.7 L14.7 4.2 C15.4 3.5 16.5 3.5 17.2 4.2 Z M6.4 16.6 L14.4 8.6";

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
                ],
                iconMargin: new Thickness(7, 1, 0, 0)),
            new MapToolItemViewModel(
                MapToolKind.Eraser,
                "Eraser",
                "E",
                EraserIconGeometry,
                buttonHints:
                [
                    new("Click Wall", "Delete Wall"),
                    new("Right-Drag", "Pan Map"),
                ],
                iconMargin: new Thickness(1, 1, 0, 0)),
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
