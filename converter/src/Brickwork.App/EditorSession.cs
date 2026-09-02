using CommunityToolkit.Mvvm.ComponentModel;
using Brickwork.Core.Geometry;
using Brickwork.Core.Models;

namespace Brickwork.App;

public sealed partial class EditorSession : ObservableObject
{
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

    public void NotifyContentChanged()
    {
        ContentRevision++;
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

    partial void OnMapChanged(MapDocument? value) => ClearWallSelection();

    partial void OnWallSimplificationToleranceChanged(double value)
    {
        if (Map is null)
        {
            return;
        }

        WallPointSimplifier.ApplyAll(Map.Walls, value);
        NotifyContentChanged();
    }
}
