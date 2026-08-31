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
    private int _contentRevision;

    [ObservableProperty]
    private double _wallSimplificationTolerance = WallSimplificationSettings.DefaultToleranceSceneUnits;

    public void NotifyContentChanged()
    {
        ContentRevision++;
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
