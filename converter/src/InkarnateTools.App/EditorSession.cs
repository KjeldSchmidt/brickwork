using CommunityToolkit.Mvvm.ComponentModel;
using InkarnateTools.Core.Geometry;
using InkarnateTools.Core.Models;

namespace InkarnateTools.App;

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
    private double _wallSimplificationTolerance = WallSimplificationSettings.DefaultToleranceSceneUnits;

    public void NotifyContentChanged()
    {
        ContentRevision++;
    }

    public void SetFocusedWall(Wall wall, WallPortal? portal = null)
    {
        FocusedWallEntityId = wall.EntityId;
        FocusedPortal = portal;
    }

    public void RequestWallTreeFocus(Wall wall, WallPortal? portal = null)
    {
        SetFocusedWall(wall, portal);
        TreeFocusGeneration++;
    }

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
