using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using InkarnateTools.App.ViewModels;

namespace InkarnateTools.App.Dock;

public sealed class AppDockFactory : Factory
{
    private readonly EditorSession _session;
    private IRootDock? _rootDock;

    public AppDockFactory(EditorSession session)
    {
        _session = session;
    }

    public override IRootDock CreateLayout()
    {
        var importTool = new ImportToolViewModel(_session)
        {
            Id = "ImportTool",
            Title = "Import",
            CanClose = false,
            CanPin = true,
        };

        var wallsTool = new WallsToolViewModel(_session)
        {
            Id = "WallsTool",
            Title = "Walls",
            CanClose = false,
            CanPin = true,
        };

        var mapPreview = new MapPreviewDocumentViewModel(_session)
        {
            Id = "MapPreview",
            Title = "Map Preview",
            CanClose = false,
        };

        var importDock = new ToolDock
        {
            ActiveDockable = importTool,
            VisibleDockables = CreateList<IDockable>(importTool),
            Alignment = Alignment.Left,
            GripMode = GripMode.Visible,
            Proportion = 0.35,
        };

        var wallsDock = new ToolDock
        {
            ActiveDockable = wallsTool,
            VisibleDockables = CreateList<IDockable>(wallsTool),
            Alignment = Alignment.Left,
            GripMode = GripMode.Visible,
            Proportion = 0.65,
        };

        var leftSidebar = new ProportionalDock
        {
            Orientation = Orientation.Vertical,
            VisibleDockables = CreateList<IDockable>(
                importDock,
                new ProportionalDockSplitter { CanResize = true, ResizePreview = true },
                wallsDock),
        };

        var documentDock = new DocumentDock
        {
            IsCollapsable = false,
            ActiveDockable = mapPreview,
            VisibleDockables = CreateList<IDockable>(mapPreview),
            CanCreateDocument = false,
        };

        var mainLayout = new ProportionalDock
        {
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(
                new ProportionalDock
                {
                    Proportion = 0.28,
                    Orientation = Orientation.Vertical,
                    VisibleDockables = CreateList<IDockable>(leftSidebar),
                },
                new ProportionalDockSplitter { CanResize = true, ResizePreview = true },
                documentDock),
        };

        var rootDock = CreateRootDock();
        rootDock.IsCollapsable = false;
        rootDock.ActiveDockable = mainLayout;
        rootDock.DefaultDockable = mainLayout;
        rootDock.VisibleDockables = CreateList<IDockable>(mainLayout);

        _rootDock = rootDock;
        return rootDock;
    }

    public override void InitLayout(IDockable layout)
    {
        ContextLocator = new Dictionary<string, Func<object?>>
        {
            ["ImportTool"] = () => _session,
            ["WallsTool"] = () => _session,
            ["MapPreview"] = () => _session,
            ["Root"] = () => layout,
        };

        DockableLocator = new Dictionary<string, Func<IDockable?>>
        {
            ["Root"] = () => _rootDock,
        };

        base.InitLayout(layout);
    }
}
