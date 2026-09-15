using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;
using Brickwork.Core.Geometry;
using Brickwork.Core.Models;

namespace Brickwork.App.ViewModels;

public partial class MapPreviewDocumentViewModel : Document
{
    private readonly EditorSession _session;
    private WallVertexPickTarget? _vertexDragTarget;
    private IDisposable? _vertexDragGesture;

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

    [ObservableProperty]
    private IReadOnlySet<int> _selectedWallEntityIds = new HashSet<int>();

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
        SelectedWallEntityIds = _session.SelectedWallEntityIds.ToHashSet();
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

    public bool IsWallEditingToolActive => _session.ActiveMapTool == MapToolKind.WallEditing;

    public void ClearWallSelection() => _session.ClearWallSelection();

    public bool DeleteSelectedWalls() => _session.DeleteSelectedWalls();

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
        if (Map is null || _session.ActiveMapTool != MapToolKind.WallEditing)
        {
            return false;
        }

        var hit = WallVertexHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8);
        if (hit is null)
        {
            return false;
        }

        _vertexDragTarget = hit;
        _vertexDragGesture = _session.BeginGesture("Move wall geometry");
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
        var gesture = _vertexDragGesture;
        _vertexDragGesture = null;
        gesture?.Dispose();
    }

    public void EditWallAt(MapPoint previewPoint, bool cycleType, bool toggleActive)
    {
        if (Map is null || _session.ActiveMapTool != MapToolKind.WallEditing)
        {
            return;
        }

        var hit = WallHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8);
        if (hit is null)
        {
            return;
        }

        var name = cycleType
            ? "Change wall type"
            : toggleActive
                ? "Toggle wall active"
                : "Edit wall";

        _session.Execute(name, () =>
        {
            if (cycleType)
            {
                WallLineEditing.CycleType(hit.Wall, hit.Portal);
            }

            if (toggleActive)
            {
                WallLineEditing.ToggleActive(hit.Wall, hit.Portal);
            }
        });

        _session.RequestWallTreeFocus(hit.Wall, hit.Portal);
    }

    public bool TryRemoveVertexAt(MapPoint previewPoint)
    {
        if (Map is null || _session.ActiveMapTool != MapToolKind.WallEditing)
        {
            return false;
        }

        var hit = WallVertexHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8);
        if (hit is null)
        {
            return false;
        }

        if (hit.VertexIndex is int vertexIndex)
        {
            var removedVertex = false;
            _session.Execute("Remove wall vertex", () =>
            {
                removedVertex = WallGeometryEditing.TryRemoveVertex(hit.Wall, vertexIndex);
            });

            if (!removedVertex)
            {
                return false;
            }

            _session.RequestWallTreeFocus(hit.Wall);
            return true;
        }

        if (hit.Portal is { } portal)
        {
            var removedPortal = false;
            _session.Execute("Remove portal", () =>
            {
                removedPortal = WallGeometryEditing.TryRemovePortal(hit.Wall, portal);
            });

            if (!removedPortal)
            {
                return false;
            }

            _session.RequestWallTreeFocus(hit.Wall);
            return true;
        }

        return false;
    }

    public bool TryInsertVertexAt(MapPoint previewPoint)
    {
        if (Map is null || _session.ActiveMapTool != MapToolKind.WallEditing)
        {
            return false;
        }

        var hit = WallHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8);
        if (hit is null)
        {
            return false;
        }

        var transform = SceneTransform.FromMap(Map);
        if (transform is null)
        {
            return false;
        }

        var scenePoint = transform.PreviewToScene(previewPoint);
        var inserted = false;
        _session.Execute("Insert wall vertex", () =>
        {
            inserted = WallGeometryEditing.TryInsertVertex(hit.Wall, scenePoint) is not null;
        });

        if (!inserted)
        {
            return false;
        }

        _session.RequestWallTreeFocus(hit.Wall, hit.Portal);
        return true;
    }

    public bool TryEraseWallAt(MapPoint previewPoint)
    {
        if (Map is null || _session.ActiveMapTool != MapToolKind.Eraser)
        {
            return false;
        }

        Wall? wall = null;
        var vertexHit = WallVertexHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8);
        if (vertexHit is not null)
        {
            wall = vertexHit.Wall;
        }
        else
        {
            wall = WallHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8)?.Wall;
        }

        if (wall is null)
        {
            return false;
        }

        var removed = false;
        _session.Execute("Delete wall", () =>
        {
            removed = WallLineEditing.RemoveFromMap(Map, wall);
        });

        if (!removed)
        {
            return false;
        }

        _session.ClearWallSelection();
        return true;
    }

    public void HandleShiftSelectClick(MapPoint previewPoint)
    {
        if (Map is null || _session.ActiveMapTool != MapToolKind.WallEditing)
        {
            return;
        }

        var hit = WallHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8)
            ?? (WallVertexHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8) is { } vertex
                ? new WallPickTarget(vertex.Wall, vertex.Portal)
                : null);

        if (hit is null)
        {
            return;
        }

        _session.ToggleWallInSelection(hit.Wall.EntityId);
        _session.TreeFocusGeneration++;
    }

    public void ApplyMarqueeSelection(MapPoint previewMin, MapPoint previewMax, bool addToSelection)
    {
        if (Map is null || _session.ActiveMapTool != MapToolKind.WallEditing)
        {
            return;
        }

        var left = Math.Min(previewMin.X, previewMax.X);
        var top = Math.Min(previewMin.Y, previewMax.Y);
        var right = Math.Max(previewMin.X, previewMax.X);
        var bottom = Math.Max(previewMin.Y, previewMax.Y);
        var ids = WallMarqueePicker.PickWallIds(Map, left, top, right, bottom);
        if (ids.Count == 0)
        {
            if (!addToSelection)
            {
                ClearWallSelection();
            }

            return;
        }

        if (addToSelection)
        {
            _session.AddWallsToSelection(ids);
        }
        else
        {
            _session.SetSelection(ids);
        }

        _session.TreeFocusGeneration++;
    }

    public void HandlePrimaryClick(MapPoint previewPoint, bool shiftSelect = false)
    {
        switch (_session.ActiveMapTool)
        {
            case MapToolKind.Eraser:
                TryEraseWallAt(previewPoint);
                break;
            case MapToolKind.WallEditing:
                if (shiftSelect)
                {
                    HandleShiftSelectClick(previewPoint);
                    break;
                }

                if (!HasWallAt(previewPoint))
                {
                    ClearWallSelection();
                }
                else
                {
                    EditWallAt(previewPoint, cycleType: true, toggleActive: false);
                }

                break;
        }
    }
}
