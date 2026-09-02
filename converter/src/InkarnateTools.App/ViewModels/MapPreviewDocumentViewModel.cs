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

    [ObservableProperty]
    private int _highlightRevision;

    [ObservableProperty]
    private int? _focusedWallEntityId;

    [ObservableProperty]
    private WallPortal? _focusedPortal;

    [ObservableProperty]
    private int? _hoveredWallEntityId;

    [ObservableProperty]
    private WallPortal? _hoveredPortal;

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

            if (args.PropertyName is nameof(EditorSession.HighlightRevision)
                or nameof(EditorSession.FocusedWallEntityId)
                or nameof(EditorSession.FocusedPortal)
                or nameof(EditorSession.HoveredWallEntityId)
                or nameof(EditorSession.HoveredPortal))
            {
                UpdateHighlightFromSession();
            }
        };
        UpdateFromSession();
        UpdateHighlightFromSession();
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

    private void UpdateHighlightFromSession()
    {
        HighlightRevision = _session.HighlightRevision;
        FocusedWallEntityId = _session.FocusedWallEntityId;
        FocusedPortal = _session.FocusedPortal;
        HoveredWallEntityId = _session.HoveredWallEntityId;
        HoveredPortal = _session.HoveredPortal;
    }

    public void UpdateHoverAt(MapPoint previewPoint)
    {
        if (Map is null)
        {
            _session.ClearHoveredWall();
            return;
        }

        var vertexHit = WallVertexHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8);
        if (vertexHit is not null)
        {
            _session.SetHoveredWall(vertexHit.Wall, vertexHit.Portal);
            return;
        }

        var wallHit = WallHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8);
        if (wallHit is not null)
        {
            _session.SetHoveredWall(wallHit.Wall, wallHit.Portal);
            return;
        }

        _session.ClearHoveredWall();
    }

    public void ClearHover() => _session.ClearHoveredWall();

    public void ClearWallSelection() => _session.ClearWallSelection();

    public bool HasWallAt(MapPoint previewPoint)
    {
        if (Map is null)
        {
            return false;
        }

        return WallVertexHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8) is not null
            || WallHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8) is not null;
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
