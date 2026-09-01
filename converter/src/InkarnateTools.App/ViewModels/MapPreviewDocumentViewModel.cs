using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;
using InkarnateTools.Core.Geometry;
using InkarnateTools.Core.Models;

namespace InkarnateTools.App.ViewModels;

public partial class MapPreviewDocumentViewModel : Document
{
    private readonly EditorSession _session;
    private WallVertexPickTarget? _vertexDragTarget;

    [ObservableProperty]
    private MapDocument? _map;

    [ObservableProperty]
    private double _previewWidth;

    [ObservableProperty]
    private double _previewHeight;

    [ObservableProperty]
    private bool _hasMap;

    [ObservableProperty]
    private bool _showPlaceholder = true;

    [ObservableProperty]
    private int _contentRevision;

    public MapPreviewDocumentViewModel(EditorSession session)
    {
        _session = session;
        _session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(EditorSession.Map))
            {
                UpdateFromSession();
            }

            if (args.PropertyName is nameof(EditorSession.ContentRevision))
            {
                ContentRevision = _session.ContentRevision;
            }
        };
        UpdateFromSession();
        ContentRevision = _session.ContentRevision;
    }

    private void UpdateFromSession()
    {
        Map = _session.Map;
        HasMap = Map is not null;
        ShowPlaceholder = !HasMap;
        PreviewWidth = Map?.Preview?.Width ?? 2048;
        PreviewHeight = Map?.Preview?.Height ?? 1536;
    }

    public bool TryBeginVertexDrag(MapPoint previewPoint)
    {
        if (Map is null)
        {
            return false;
        }

        var hit = WallVertexHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8);
        if (hit is null)
        {
            return false;
        }

        _vertexDragTarget = hit;
        _session.RequestWallTreeFocus(hit.Wall, hit.Portal);
        return true;
    }

    public void DragVertexTo(MapPoint previewPoint)
    {
        if (Map is null || _vertexDragTarget is null)
        {
            return;
        }

        var transform = SceneTransform.FromMap(Map);
        if (transform is null)
        {
            return;
        }

        var scenePoint = transform.PreviewToScene(previewPoint);
        var target = _vertexDragTarget;

        if (target.VertexIndex is int vertexIndex)
        {
            WallGeometryEditing.SetVertexPosition(target.Wall, vertexIndex, scenePoint);
        }
        else if (target.Portal is { } portal)
        {
            if (target.PortalWidthEndpoint is { } endpoint)
            {
                WallGeometryEditing.SetPortalEndpointFromScene(target.Wall, portal, endpoint, scenePoint);
            }
            else
            {
                WallGeometryEditing.SetPortalAnchorFromScene(target.Wall, portal, scenePoint);
            }
        }

        _session.NotifyContentChanged();
    }

    public void EndVertexDrag()
    {
        _vertexDragTarget = null;
    }

    public void EditWallAt(MapPoint previewPoint, bool cycleType, bool toggleActive)
    {
        if (Map is null)
        {
            return;
        }

        var hit = WallHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8);
        if (hit is null)
        {
            return;
        }

        if (cycleType)
        {
            WallLineEditing.CycleType(hit.Wall, hit.Portal);
        }

        if (toggleActive)
        {
            WallLineEditing.ToggleActive(hit.Wall, hit.Portal);
        }

        _session.RequestWallTreeFocus(hit.Wall, hit.Portal);
        _session.NotifyContentChanged();
    }
}
