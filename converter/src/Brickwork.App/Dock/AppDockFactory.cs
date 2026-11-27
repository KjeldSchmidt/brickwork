using System.Collections.ObjectModel;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using Brickwork.App.ViewModels;

namespace Brickwork.App.Dock;

public sealed class AppDockFactory : Factory
{
    private const double FileDockCompactProportion = 0.17;
    private const double FileDockExpandedProportion = 0.27;
    private const double WallsDockCompactProportion = 1d - FileDockCompactProportion;
    private const double WallsDockExpandedProportion = 1d - FileDockExpandedProportion;

    private readonly EditorSession _session;
    private IRootDock? _rootDock;
    private ToolDock? _wallsDock;
    private DebugToolViewModel? _debugTool;

    public AppDockFactory(EditorSession session)
    {
        _session = session;
    }

    public ImportToolViewModel? ImportTool { get; private set; }

    public override IRootDock CreateLayout()
    {
        var importTool = new ImportToolViewModel(_session)
        {
            Id = "ImportTool",
            Title = "File",
            CanClose = false,
            CanPin = true,
        };
        ImportTool = importTool;

        var wallsTool = new WallsToolViewModel(_session)
        {
            Id = "WallsTool",
            Title = "Walls",
            CanClose = false,
            CanPin = true,
        };

        var regionsTool = new RegionsToolViewModel(_session)
        {
            Id = "RegionsTool",
            Title = "Regions",
            CanClose = false,
            CanPin = true,
        };

        var lightsTool = new LightsToolViewModel(_session)
        {
            Id = EditorSession.LightsPanelId,
            Title = "Lights",
            CanClose = false,
            CanPin = true,
        };

        var treesTool = new TreesToolViewModel(_session)
        {
            Id = EditorSession.TreesPanelId,
            Title = "Trees",
            CanClose = false,
            CanPin = true,
        };

        _debugTool = new DebugToolViewModel(_session)
        {
            Id = "DebugTool",
            Title = "Debug",
            CanClose = false,
            CanPin = true,
        };

        var mapPreview = new MapPreviewDocumentViewModel(_session)
        {
            Id = "MapPreview",
            Title = "Map Preview",
            CanClose = false,
        };

        var toolPalette = new ToolPaletteViewModel(_session)
        {
            Id = "ToolPalette",
            Title = "Tools",
            CanClose = false,
            CanPin = false,
            CanFloat = true,
            MinWidth = 40,
            MaxWidth = 40,
        };

        var importDock = new ToolDock
        {
            ActiveDockable = importTool,
            VisibleDockables = CreateList<IDockable>(importTool),
            Alignment = Alignment.Left,
            GripMode = GripMode.Visible,
            Proportion = FileDockCompactProportion,
        };

        var wallsDockables = new ObservableCollection<IDockable> { wallsTool, regionsTool, lightsTool, treesTool };
        if (_session.ShowDebugPanel)
        {
            wallsDockables.Add(_debugTool);
        }

        _wallsDock = new ToolDock
        {
            ActiveDockable = wallsTool,
            VisibleDockables = wallsDockables,
            Alignment = Alignment.Left,
            GripMode = GripMode.Visible,
            Proportion = WallsDockCompactProportion,
        };

        _session.ActivePanelId = _wallsDock.ActiveDockable?.Id;
        _wallsDock.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(ToolDock.ActiveDockable))
            {
                _session.ActivePanelId = _wallsDock.ActiveDockable?.Id;
            }
        };

        importTool.FileDockExpandedChanged = expanded =>
        {
            importDock.Proportion = expanded ? FileDockExpandedProportion : FileDockCompactProportion;
            _wallsDock.Proportion = expanded ? WallsDockExpandedProportion : WallsDockCompactProportion;
        };

        _session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(EditorSession.ActiveMapTool))
            {
                _wallsDock.ActiveDockable = _session.ActiveMapTool switch
                {
                    MapToolKind.WallEditing => wallsTool,
                    MapToolKind.RegionEditing => regionsTool,
                    _ => _wallsDock.ActiveDockable,
                };
            }

            if (args.PropertyName is nameof(EditorSession.ShowDebugPanel))
            {
                ApplyDebugPanelVisibility();
            }
        };

        var leftSidebar = new ProportionalDock
        {
            Column = 0,
            Orientation = Orientation.Vertical,
            VisibleDockables = CreateList<IDockable>(
                importDock,
                new ProportionalDockSplitter { CanResize = true, ResizePreview = false },
                _wallsDock),
        };

        var toolsDock = new ToolDock
        {
            Column = 1,
            ActiveDockable = toolPalette,
            VisibleDockables = CreateList<IDockable>(toolPalette),
            Alignment = Alignment.Left,
            GripMode = GripMode.Hidden,
            MinWidth = 40,
            MaxWidth = 40,
        };

        var documentDock = new DocumentDock
        {
            IsCollapsable = false,
            ActiveDockable = mapPreview,
            VisibleDockables = CreateList<IDockable>(mapPreview),
            CanCreateDocument = false,
        };

        var leftChrome = new GridDock
        {
            Proportion = 0.28,
            ColumnDefinitions = "*,40",
            VisibleDockables = CreateList<IDockable>(leftSidebar, toolsDock),
        };

        var mainLayout = new ProportionalDock
        {
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(
                leftChrome,
                new ProportionalDockSplitter { CanResize = true, ResizePreview = false },
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
            ["RegionsTool"] = () => _session,
            ["LightsTool"] = () => _session,
            ["TreesTool"] = () => _session,
            ["DebugTool"] = () => _session,
            ["ToolPalette"] = () => _session,
            ["MapPreview"] = () => _session,
            ["Root"] = () => layout,
        };

        DockableLocator = new Dictionary<string, Func<IDockable?>>
        {
            ["Root"] = () => _rootDock,
        };

        base.InitLayout(layout);
    }

    private void ApplyDebugPanelVisibility()
    {
        if (_wallsDock?.VisibleDockables is null || _debugTool is null)
        {
            return;
        }

        var dockables = _wallsDock.VisibleDockables;
        var isPresent = dockables.Contains(_debugTool);

        if (_session.ShowDebugPanel && !isPresent)
        {
            dockables.Add(_debugTool);
        }
        else if (!_session.ShowDebugPanel && isPresent)
        {
            if (ReferenceEquals(_wallsDock.ActiveDockable, _debugTool))
            {
                _wallsDock.ActiveDockable = dockables.FirstOrDefault(d => !ReferenceEquals(d, _debugTool));
            }

            dockables.Remove(_debugTool);
        }
    }
}
