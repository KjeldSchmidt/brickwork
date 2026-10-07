using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;
using Brickwork.Core.Geometry;
using Brickwork.Core.Models;

namespace Brickwork.App.ViewModels;

public partial class MapPreviewDocumentViewModel : Document
{
    private readonly EditorSession _session;
    private WallVertexPickTarget? _vertexDragTarget;
    private RegionVertexPickTarget? _regionVertexDragTarget;
    private IDisposable? _vertexDragGesture;
    private IReadOnlyList<Wall>? _movingWalls;
    private IReadOnlyList<Region>? _movingRegions;
    private MapPoint? _entityMoveLastScenePoint;
    private IDisposable? _entityMoveGesture;
    private Wall? _drawingWall;
    private Region? _drawingRegion;
    private IDisposable? _drawingGesture;
    private IDisposable? _eraserGesture;
    private MapPoint? _lastErasePreviewPoint;

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

    [ObservableProperty]
    private int? _focusedRegionEntityId;

    [ObservableProperty]
    private int? _hoveredRegionEntityId;

    [ObservableProperty]
    private IReadOnlySet<int> _selectedRegionEntityIds = new HashSet<int>();

    public MapPreviewDocumentViewModel(EditorSession session)
    {
        _session = session;
        _session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(EditorSession.Map))
            {
                _drawingWall = null;
                _drawingRegion = null;
                _drawingGesture = null;
                _vertexDragTarget = null;
                _regionVertexDragTarget = null;
                _vertexDragGesture = null;
                EndEntityMove();
                EndEraserStroke();
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
                or nameof(EditorSession.HoveredPortal)
                or nameof(EditorSession.FocusedRegionEntityId)
                or nameof(EditorSession.HoveredRegionEntityId))
            {
                UpdateHighlightFromSession();
            }

            if (args.PropertyName is nameof(EditorSession.ActiveMapTool))
            {
                OnPropertyChanged(nameof(IsWallEditingToolActive));
                OnPropertyChanged(nameof(IsRegionEditingToolActive));
                OnPropertyChanged(nameof(IsEraserToolActive));
                // Handles visibility is tool-gated; force a redraw.
                HighlightRevision = _session.HighlightRevision;
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
        FocusedRegionEntityId = _session.FocusedRegionEntityId;
        HoveredRegionEntityId = _session.HoveredRegionEntityId;
        SelectedRegionEntityIds = _session.SelectedRegionEntityIds.ToHashSet();
    }

    public void UpdateHoverAt(MapPoint previewPoint)
    {
        if (Map is null)
        {
            _session.ClearHover();
            return;
        }

        switch (_session.ActiveMapTool)
        {
            case MapToolKind.RegionEditing:
                UpdateRegionHoverAt(previewPoint);
                return;
            case MapToolKind.Eraser:
                UpdateEraserHoverAt(previewPoint);
                return;
            default:
                UpdateWallHoverAt(previewPoint);
                return;
        }
    }

    private void UpdateWallHoverAt(MapPoint previewPoint)
    {
        if (Map is null)
        {
            _session.ClearHover();
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

        _session.ClearHover();
    }

    private void UpdateRegionHoverAt(MapPoint previewPoint)
    {
        if (Map is null)
        {
            _session.ClearHover();
            return;
        }

        var vertexHit = RegionVertexHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8);
        if (vertexHit is not null)
        {
            _session.SetHoveredRegion(vertexHit.Region);
            return;
        }

        var regionHit = RegionHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8);
        if (regionHit is not null)
        {
            _session.SetHoveredRegion(regionHit.Region);
            return;
        }

        _session.ClearHover();
    }

    private void UpdateEraserHoverAt(MapPoint previewPoint)
    {
        if (Map is null)
        {
            _session.ClearHover();
            return;
        }

        var wallVertex = WallVertexHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8);
        var regionVertex = RegionVertexHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8);
        if (wallVertex is not null)
        {
            _session.SetHoveredWall(wallVertex.Wall, wallVertex.Portal);
            return;
        }

        if (regionVertex is not null)
        {
            _session.SetHoveredRegion(regionVertex.Region);
            return;
        }

        var wallHit = WallHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8);
        var regionHit = RegionHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8);
        if (wallHit is not null)
        {
            _session.SetHoveredWall(wallHit.Wall, wallHit.Portal);
            return;
        }

        if (regionHit is not null)
        {
            _session.SetHoveredRegion(regionHit.Region);
            return;
        }

        _session.ClearHover();
    }

    public void ClearHover() => _session.ClearHover();

    public bool IsWallEditingToolActive => _session.ActiveMapTool == MapToolKind.WallEditing;

    public bool IsRegionEditingToolActive => _session.ActiveMapTool == MapToolKind.RegionEditing;

    public bool IsEraserToolActive => _session.ActiveMapTool == MapToolKind.Eraser;

    public bool IsDrawingWall => _drawingWall is not null;

    public bool IsDrawingRegion => _drawingRegion is not null;

    public bool IsDrawing => IsDrawingWall || IsDrawingRegion;

    public void ClearWallSelection() => _session.ClearWallSelection();

    public void ClearRegionSelection() => _session.ClearRegionSelection();

    public void ClearSelection()
    {
        _session.ClearWallSelection();
        _session.ClearRegionSelection();
    }

    public bool DeleteSelectedWalls() => _session.DeleteSelectedWalls();

    public bool DeleteSelection() => _session.DeleteSelection();

    public bool HasWallAt(MapPoint previewPoint)
    {
        if (Map is null)
        {
            return false;
        }

        return WallVertexHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8) is not null
            || WallHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8) is not null;
    }

    public bool HasRegionAt(MapPoint previewPoint)
    {
        if (Map is null)
        {
            return false;
        }

        return RegionVertexHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8) is not null
            || RegionHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8) is not null;
    }

    public bool TryBeginVertexDrag(MapPoint previewPoint)
    {
        if (Map is null || IsDrawing)
        {
            return false;
        }

        if (_session.ActiveMapTool == MapToolKind.RegionEditing)
        {
            var regionHit = RegionVertexHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8);
            if (regionHit is null)
            {
                return false;
            }

            _regionVertexDragTarget = regionHit;
            _vertexDragTarget = null;
            _vertexDragGesture = _session.BeginGesture("Move region geometry");
            _session.RequestRegionTreeFocus(regionHit.Region);
            return true;
        }

        if (_session.ActiveMapTool != MapToolKind.WallEditing)
        {
            return false;
        }

        var hit = WallVertexHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8);
        if (hit is null)
        {
            return false;
        }

        _vertexDragTarget = hit;
        _regionVertexDragTarget = null;
        _vertexDragGesture = _session.BeginGesture("Move wall geometry");
        _session.RequestWallTreeFocus(hit.Wall, hit.Portal);
        return true;
    }

    public void DragVertexTo(MapPoint previewPoint)
    {
        if (Map is null)
        {
            return;
        }

        var transform = SceneTransform.FromMap(Map);
        if (transform is null)
        {
            return;
        }

        var scenePoint = transform.PreviewToScene(previewPoint);

        if (_regionVertexDragTarget is { } regionTarget)
        {
            RegionGeometryEditing.SetVertexPosition(
                regionTarget.Region,
                regionTarget.VertexIndex,
                scenePoint);
            _session.NotifyContentChanged();
            return;
        }

        if (_vertexDragTarget is null)
        {
            return;
        }

        var target = _vertexDragTarget;

        if (target.VertexIndex is int vertexIndex)
        {
            WallGeometryEditing.SetVertexPosition(target.Wall, vertexIndex, scenePoint);
        }
        else if (target.TerrainThicknessEndpoint is not null)
        {
            WallGeometryEditing.SetTerrainThicknessFromScene(target.Wall, scenePoint);
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
        _regionVertexDragTarget = null;
        var gesture = _vertexDragGesture;
        _vertexDragGesture = null;
        gesture?.Dispose();
    }

    public bool CanBeginEntityMoveAt(MapPoint previewPoint)
    {
        if (Map is null || IsDrawing)
        {
            return false;
        }

        return _session.ActiveMapTool switch
        {
            MapToolKind.WallEditing => WallHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8) is not null,
            MapToolKind.RegionEditing => RegionHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8) is not null,
            _ => false,
        };
    }

    public bool TryBeginEntityMove(MapPoint previewPoint)
    {
        if (Map is null || IsDrawing)
        {
            return false;
        }

        var transform = SceneTransform.FromMap(Map);
        if (transform is null)
        {
            return false;
        }

        if (_session.ActiveMapTool == MapToolKind.RegionEditing)
        {
            var regionHit = RegionHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8);
            if (regionHit is null)
            {
                return false;
            }

            var selectedIds = _session.SelectedRegionEntityIds.ToHashSet();
            var moveSelection = selectedIds.Contains(regionHit.Region.EntityId) && selectedIds.Count > 0;
            _movingRegions = moveSelection
                ? Map.Regions.Where(region => selectedIds.Contains(region.EntityId)).ToList()
                : [regionHit.Region];
            _movingWalls = null;
            _entityMoveLastScenePoint = transform.PreviewToScene(previewPoint);
            _entityMoveGesture = _session.BeginGesture(
                _movingRegions.Count > 1 ? "Move regions" : "Move region");

            if (moveSelection)
            {
                _session.SetRegionSelection(selectedIds, regionHit.Region.EntityId);
                _session.TreeFocusGeneration++;
            }
            else
            {
                _session.RequestRegionTreeFocus(regionHit.Region);
            }

            return true;
        }

        if (_session.ActiveMapTool != MapToolKind.WallEditing)
        {
            return false;
        }

        var wallHit = WallHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8);
        if (wallHit is null)
        {
            return false;
        }

        var wallIds = _session.SelectedWallEntityIds.ToHashSet();
        var moveWallSelection = wallIds.Contains(wallHit.Wall.EntityId) && wallIds.Count > 0;
        _movingWalls = moveWallSelection
            ? Map.Walls.Where(wall => wallIds.Contains(wall.EntityId)).ToList()
            : [wallHit.Wall];
        _movingRegions = null;
        _entityMoveLastScenePoint = transform.PreviewToScene(previewPoint);
        _entityMoveGesture = _session.BeginGesture(
            _movingWalls.Count > 1 ? "Move walls" : "Move wall");

        if (moveWallSelection)
        {
            // Keep multi-selection; only update primary focus for highlights/tree.
            _session.SetPrimaryFocus(wallHit.Wall.EntityId, wallHit.Portal);
            _session.TreeFocusGeneration++;
        }
        else
        {
            _session.RequestWallTreeFocus(wallHit.Wall, wallHit.Portal);
        }

        return true;
    }

    public void DragEntityTo(MapPoint previewPoint)
    {
        if (Map is null || _entityMoveLastScenePoint is not { } lastScene)
        {
            return;
        }

        var transform = SceneTransform.FromMap(Map);
        if (transform is null)
        {
            return;
        }

        var scenePoint = transform.PreviewToScene(previewPoint);
        var dx = scenePoint.X - lastScene.X;
        var dy = scenePoint.Y - lastScene.Y;
        if (Math.Abs(dx) <= 1e-9 && Math.Abs(dy) <= 1e-9)
        {
            return;
        }

        if (_movingWalls is { Count: > 0 })
        {
            foreach (var wall in _movingWalls)
            {
                WallGeometryEditing.Translate(wall, dx, dy);
            }
        }
        else if (_movingRegions is { Count: > 0 })
        {
            foreach (var region in _movingRegions)
            {
                RegionGeometryEditing.Translate(region, dx, dy);
            }
        }
        else
        {
            return;
        }

        _entityMoveLastScenePoint = scenePoint;
        _session.NotifyContentChanged();
    }

    public void EndEntityMove()
    {
        _movingWalls = null;
        _movingRegions = null;
        _entityMoveLastScenePoint = null;
        var gesture = _entityMoveGesture;
        _entityMoveGesture = null;
        gesture?.Dispose();
    }

    public bool TryBeginPortalCreateDrag(MapPoint previewPoint)
    {
        if (Map is null || _session.ActiveMapTool != MapToolKind.WallEditing || IsDrawingWall)
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
        _vertexDragGesture = _session.BeginGesture("Add portal");
        WallPortal? portal = null;
        _session.Execute("Add portal", () =>
        {
            portal = WallGeometryEditing.TryAddPortal(hit.Wall, scenePoint, defaultWidth: 2d);
        });

        if (portal is null)
        {
            _vertexDragGesture = null;
            _session.CancelActiveGesture();
            return false;
        }

        _vertexDragTarget = new WallVertexPickTarget(hit.Wall, null, portal, PortalWidthEndpoint.End);
        _session.RequestWallTreeFocus(hit.Wall, portal);
        return true;
    }

    public bool TryStartDrawingWall(MapPoint previewPoint)
    {
        if (Map is null || _session.ActiveMapTool != MapToolKind.WallEditing || IsDrawing)
        {
            return false;
        }

        if (HasWallAt(previewPoint))
        {
            return false;
        }

        var transform = SceneTransform.FromMap(Map);
        if (transform is null)
        {
            return false;
        }

        var scenePoint = transform.PreviewToScene(previewPoint);
        var entityId = RegionConversion.AllocateEntityId(Map);

        var wall = new Wall
        {
            EntityId = entityId,
            Name = "Wall",
            LineType = WallLineType.Solid,
            IsActive = true,
            WallEnabled = true,
            WallThickness = WallLineEditing.DefaultTerrainWallThickness,
            Points = { scenePoint, scenePoint },
        };

        _drawingGesture = _session.BeginGesture("Add wall");
        _session.Execute("Add wall", () =>
        {
            wall.LayerId = MapLayerEditing.EnsureDefaultLayer(Map).Id;
            Map.Walls.Add(wall);
        });

        _drawingWall = wall;
        _session.RequestWallTreeFocus(wall);
        return true;
    }

    public bool TryStartDrawingRegion(MapPoint previewPoint)
    {
        if (Map is null || _session.ActiveMapTool != MapToolKind.RegionEditing || IsDrawing)
        {
            return false;
        }

        if (HasRegionAt(previewPoint))
        {
            return false;
        }

        var transform = SceneTransform.FromMap(Map);
        if (transform is null)
        {
            return false;
        }

        var scenePoint = transform.PreviewToScene(previewPoint);
        var entityId = RegionConversion.AllocateEntityId(Map);
        var region = new Region
        {
            EntityId = entityId,
            Name = "Region",
            IsActive = true,
            RegionType = RegionType.DifficultTerrain,
            Points = { scenePoint, scenePoint },
        };

        _drawingGesture = _session.BeginGesture("Add region");
        _session.Execute("Add region", () =>
        {
            region.LayerId = MapLayerEditing.EnsureDefaultLayer(Map).Id;
            Map.Regions.Add(region);
        });

        _drawingRegion = region;
        _session.RequestRegionTreeFocus(region);
        return true;
    }

    public void UpdateDrawingWallPreview(MapPoint previewPoint)
    {
        if (Map is null || _drawingWall is null || _drawingWall.Points.Count == 0)
        {
            return;
        }

        var transform = SceneTransform.FromMap(Map);
        if (transform is null)
        {
            return;
        }

        _drawingWall.Points[^1] = transform.PreviewToScene(previewPoint);
        _session.NotifyContentChanged();
    }

    public void UpdateDrawingRegionPreview(MapPoint previewPoint)
    {
        if (Map is null || _drawingRegion is null || _drawingRegion.Points.Count == 0)
        {
            return;
        }

        var transform = SceneTransform.FromMap(Map);
        if (transform is null)
        {
            return;
        }

        _drawingRegion.Points[^1] = transform.PreviewToScene(previewPoint);
        _session.NotifyContentChanged();
    }

    public void CommitDrawingWallVertex(MapPoint previewPoint)
    {
        if (Map is null || _drawingWall is null)
        {
            return;
        }

        var transform = SceneTransform.FromMap(Map);
        if (transform is null)
        {
            return;
        }

        var scenePoint = transform.PreviewToScene(previewPoint);
        _drawingWall.Points[^1] = scenePoint;
        _drawingWall.Points.Add(scenePoint);
        _session.NotifyContentChanged();
        _session.RequestWallTreeFocus(_drawingWall);
    }

    public void CommitDrawingRegionVertex(MapPoint previewPoint)
    {
        if (Map is null || _drawingRegion is null)
        {
            return;
        }

        var transform = SceneTransform.FromMap(Map);
        if (transform is null)
        {
            return;
        }

        var scenePoint = transform.PreviewToScene(previewPoint);
        _drawingRegion.Points[^1] = scenePoint;
        _drawingRegion.Points.Add(scenePoint);
        _session.NotifyContentChanged();
        _session.RequestRegionTreeFocus(_drawingRegion);
    }

    public bool TryFinishDrawingWall()
    {
        if (Map is null || _drawingWall is null || _drawingWall.Points.Count == 0)
        {
            return false;
        }

        var transform = SceneTransform.FromMap(Map);
        if (transform is null)
        {
            return false;
        }

        return TryFinishDrawingWall(transform.SceneToPreview(_drawingWall.Points[^1]));
    }

    public bool TryFinishDrawingRegion()
    {
        if (Map is null || _drawingRegion is null || _drawingRegion.Points.Count == 0)
        {
            return false;
        }

        var transform = SceneTransform.FromMap(Map);
        if (transform is null)
        {
            return false;
        }

        return TryFinishDrawingRegion(transform.SceneToPreview(_drawingRegion.Points[^1]));
    }

    public bool TryFinishDrawing()
    {
        if (IsDrawingWall)
        {
            return TryFinishDrawingWall();
        }

        if (IsDrawingRegion)
        {
            return TryFinishDrawingRegion();
        }

        return false;
    }

    public bool TryFinishDrawingWall(MapPoint previewPoint)
    {
        if (Map is null || _drawingWall is null)
        {
            return false;
        }

        var transform = SceneTransform.FromMap(Map);
        if (transform is null)
        {
            return false;
        }

        var scenePoint = transform.PreviewToScene(previewPoint);
        var firstPreview = transform.SceneToPreview(_drawingWall.Points[0]);
        var close =
            DistanceSquared(firstPreview, previewPoint) <= 8d * 8d &&
            _drawingWall.Points.Count >= 4;

        // A finish with only the start + rubber-band is almost certainly accidental —
        // place the vertex and keep drawing instead of creating a degenerate wall.
        if (!close && _drawingWall.Points.Count < 3)
        {
            CommitDrawingWallVertex(previewPoint);
            return true;
        }

        if (close)
        {
            _drawingWall.Points.RemoveAt(_drawingWall.Points.Count - 1);
            _drawingWall.IsClosed = true;
        }
        else
        {
            _drawingWall.Points[^1] = scenePoint;
        }

        if (_drawingWall.Points.Count < 2)
        {
            CommitDrawingWallVertex(previewPoint);
            return true;
        }

        var wall = _drawingWall;
        _drawingWall = null;
        var gesture = _drawingGesture;
        _drawingGesture = null;
        gesture?.Dispose();
        _session.RequestWallTreeFocus(wall);
        return true;
    }

    public bool TryFinishDrawingRegion(MapPoint previewPoint)
    {
        if (Map is null || _drawingRegion is null)
        {
            return false;
        }

        var transform = SceneTransform.FromMap(Map);
        if (transform is null)
        {
            return false;
        }

        var firstPreview = transform.SceneToPreview(_drawingRegion.Points[0]);
        var snapClose =
            DistanceSquared(firstPreview, previewPoint) <= 8d * 8d &&
            _drawingRegion.Points.Count >= 4;

        // Too few vertices for a closed region: treat finish as placing the current
        // node and keep drawing (avoids vanishing on an early finish click).
        if (_drawingRegion.Points.Count < 4)
        {
            CommitDrawingRegionVertex(previewPoint);
            return true;
        }

        var scenePoint = transform.PreviewToScene(previewPoint);
        if (snapClose)
        {
            _drawingRegion.Points.RemoveAt(_drawingRegion.Points.Count - 1);
        }
        else
        {
            _drawingRegion.Points[^1] = scenePoint;
        }

        if (_drawingRegion.Points.Count < 3)
        {
            CommitDrawingRegionVertex(previewPoint);
            return true;
        }

        var region = _drawingRegion;
        _drawingRegion = null;
        var gesture = _drawingGesture;
        _drawingGesture = null;
        gesture?.Dispose();
        _session.RequestRegionTreeFocus(region);
        return true;
    }

    public void CancelDrawingWall()
    {
        if (_drawingWall is null && _drawingGesture is null)
        {
            return;
        }

        _drawingWall = null;
        _drawingGesture = null;
        _session.CancelActiveGesture();
        ClearWallSelection();
    }

    public void CancelDrawingRegion()
    {
        if (_drawingRegion is null && _drawingGesture is null)
        {
            return;
        }

        _drawingRegion = null;
        _drawingGesture = null;
        _session.CancelActiveGesture();
        ClearRegionSelection();
    }

    public void CancelDrawing()
    {
        if (IsDrawingWall)
        {
            CancelDrawingWall();
            return;
        }

        if (IsDrawingRegion)
        {
            CancelDrawingRegion();
        }
    }

    public void EditWallAt(MapPoint previewPoint, bool cycleType)
    {
        if (Map is null || _session.ActiveMapTool != MapToolKind.WallEditing || !cycleType || IsDrawing)
        {
            return;
        }

        var hit = WallHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8);
        if (hit is null)
        {
            return;
        }

        if (hit.Portal is not null)
        {
            _session.Execute("Change wall type", () =>
            {
                WallLineEditing.CycleType(hit.Wall, hit.Portal);
            });
            _session.RequestWallTreeFocus(hit.Wall, hit.Portal);
            return;
        }

        var selectedIds = _session.SelectedWallEntityIds;
        var nextType = WallLineEditing.CycleType(hit.Wall.LineType);
        var targets = selectedIds.Contains(hit.Wall.EntityId)
            ? Map.Walls.Where(wall => selectedIds.Contains(wall.EntityId)).ToList()
            : [hit.Wall];

        _session.Execute(
            targets.Count > 1 ? "Change wall types" : "Change wall type",
            () =>
            {
                foreach (var wall in targets)
                {
                    WallLineEditing.SetLineType(wall, nextType);
                }
            });

        // Keep multi-selection when cycling a wall that is already selected.
        if (selectedIds.Contains(hit.Wall.EntityId) && selectedIds.Count > 1)
        {
            _session.SetSelection(selectedIds, hit.Wall.EntityId);
            return;
        }

        _session.RequestWallTreeFocus(hit.Wall);
    }

    public bool TryAddPortalAt(MapPoint previewPoint)
    {
        if (Map is null || _session.ActiveMapTool != MapToolKind.WallEditing || IsDrawingWall)
        {
            return false;
        }

        var hit = WallHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8)
            ?? (WallVertexHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8) is { } vertex
                ? new WallPickTarget(vertex.Wall, null)
                : null);
        if (hit is null)
        {
            return false;
        }

        var transform = SceneTransform.FromMap(Map);
        if (transform is null)
        {
            return false;
        }

        var defaultWidth = ResolveDefaultPortalWidth(Map);
        var scenePoint = transform.PreviewToScene(previewPoint);
        WallPortal? portal = null;
        _session.Execute("Add portal", () =>
        {
            portal = WallGeometryEditing.TryAddPortal(hit.Wall, scenePoint, defaultWidth);
        });

        if (portal is null)
        {
            return false;
        }

        _session.RequestWallTreeFocus(hit.Wall, portal);
        return true;
    }

    private static double ResolveDefaultPortalWidth(MapDocument map)
    {
        var cell = map.Grid.CellSize > 0 ? map.Grid.CellSize : 1d;
        return Math.Max(cell, 2d);
    }

    private static double DistanceSquared(MapPoint a, MapPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    public bool TryRemoveVertexAt(MapPoint previewPoint)
    {
        if (Map is null)
        {
            return false;
        }

        if (_session.ActiveMapTool == MapToolKind.RegionEditing)
        {
            var regionHit = RegionVertexHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8);
            if (regionHit is null)
            {
                return false;
            }

            var removedRegionVertex = false;
            _session.Execute("Remove region vertex", () =>
            {
                removedRegionVertex = RegionGeometryEditing.TryRemoveVertex(
                    regionHit.Region,
                    regionHit.VertexIndex);
            });

            if (!removedRegionVertex)
            {
                return false;
            }

            _session.RequestRegionTreeFocus(regionHit.Region);
            return true;
        }

        if (_session.ActiveMapTool != MapToolKind.WallEditing)
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
        if (Map is null)
        {
            return false;
        }

        var transform = SceneTransform.FromMap(Map);
        if (transform is null)
        {
            return false;
        }

        var scenePoint = transform.PreviewToScene(previewPoint);

        if (_session.ActiveMapTool == MapToolKind.RegionEditing)
        {
            var regionHit = RegionHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8);
            if (regionHit is null)
            {
                return false;
            }

            var insertedRegion = false;
            _session.Execute("Insert region vertex", () =>
            {
                insertedRegion = RegionGeometryEditing.TryInsertVertex(regionHit.Region, scenePoint) is not null;
            });

            if (!insertedRegion)
            {
                return false;
            }

            _session.RequestRegionTreeFocus(regionHit.Region);
            return true;
        }

        if (_session.ActiveMapTool != MapToolKind.WallEditing)
        {
            return false;
        }

        var hit = WallHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8);
        if (hit is null)
        {
            return false;
        }

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

    public bool TryEraseAt(MapPoint previewPoint)
    {
        if (Map is null || _session.ActiveMapTool != MapToolKind.Eraser)
        {
            return false;
        }

        var wallVertex = WallVertexHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8);
        if (wallVertex is not null)
        {
            return EraseWall(wallVertex.Wall);
        }

        var regionVertex = RegionVertexHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8);
        if (regionVertex is not null)
        {
            return EraseRegion(regionVertex.Region);
        }

        var wall = WallHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8)?.Wall;
        if (wall is not null)
        {
            return EraseWall(wall);
        }

        var region = RegionHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8)?.Region;
        if (region is not null)
        {
            return EraseRegion(region);
        }

        return false;
    }

    private bool EraseWall(Wall wall)
    {
        if (Map is null)
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

    private bool EraseRegion(Region region)
    {
        if (Map is null)
        {
            return false;
        }

        var removed = false;
        _session.Execute("Delete region", () =>
        {
            removed = RegionEditing.RemoveFromMap(Map, region);
        });

        if (!removed)
        {
            return false;
        }

        _session.ClearRegionSelection();
        return true;
    }

    public bool TryBeginEraserStroke(MapPoint previewPoint)
    {
        if (Map is null || _session.ActiveMapTool != MapToolKind.Eraser)
        {
            return false;
        }

        _eraserGesture = _session.BeginGesture("Erase");
        _lastErasePreviewPoint = previewPoint;
        TryEraseAt(previewPoint);
        return true;
    }

    public void ContinueEraserStroke(MapPoint previewPoint)
    {
        if (_eraserGesture is null || Map is null || _session.ActiveMapTool != MapToolKind.Eraser)
        {
            return;
        }

        if (_lastErasePreviewPoint is { } last)
        {
            var dx = previewPoint.X - last.X;
            var dy = previewPoint.Y - last.Y;
            var distance = Math.Sqrt((dx * dx) + (dy * dy));
            var steps = Math.Max(1, (int)Math.Ceiling(distance / 4d));
            for (var i = 1; i <= steps; i++)
            {
                var t = i / (double)steps;
                TryEraseAt(new MapPoint(last.X + (dx * t), last.Y + (dy * t)));
            }
        }
        else
        {
            TryEraseAt(previewPoint);
        }

        _lastErasePreviewPoint = previewPoint;
    }

    public void EndEraserStroke()
    {
        var gesture = _eraserGesture;
        _eraserGesture = null;
        _lastErasePreviewPoint = null;
        gesture?.Dispose();
    }

    public void HandleShiftSelectClick(MapPoint previewPoint)
    {
        if (Map is null)
        {
            return;
        }

        if (_session.ActiveMapTool == MapToolKind.RegionEditing)
        {
            var regionHit = RegionHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8)
                ?? (RegionVertexHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8) is { } regionVertex
                    ? new RegionPickTarget(regionVertex.Region)
                    : null);
            if (regionHit is null)
            {
                return;
            }

            _session.ToggleRegionInSelection(regionHit.Region.EntityId);
            _session.TreeFocusGeneration++;
            return;
        }

        if (_session.ActiveMapTool != MapToolKind.WallEditing)
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
        if (Map is null)
        {
            return;
        }

        var left = Math.Min(previewMin.X, previewMax.X);
        var top = Math.Min(previewMin.Y, previewMax.Y);
        var right = Math.Max(previewMin.X, previewMax.X);
        var bottom = Math.Max(previewMin.Y, previewMax.Y);

        if (_session.ActiveMapTool == MapToolKind.RegionEditing)
        {
            var regionIds = RegionMarqueePicker.PickRegionIds(Map, left, top, right, bottom);
            if (regionIds.Count == 0)
            {
                if (!addToSelection)
                {
                    ClearRegionSelection();
                }

                return;
            }

            if (addToSelection)
            {
                _session.AddRegionsToSelection(regionIds);
            }
            else
            {
                _session.SetRegionSelection(regionIds);
            }

            _session.TreeFocusGeneration++;
            return;
        }

        if (_session.ActiveMapTool != MapToolKind.WallEditing)
        {
            return;
        }

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

    public void EditRegionAt(MapPoint previewPoint, bool cycleType)
    {
        if (Map is null || _session.ActiveMapTool != MapToolKind.RegionEditing || !cycleType || IsDrawing)
        {
            return;
        }

        var hit = RegionHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8)
            ?? (RegionVertexHitTester.Pick(Map, previewPoint, tolerancePreviewPixels: 8) is { } vertex
                ? new RegionPickTarget(vertex.Region)
                : null);
        if (hit is null)
        {
            return;
        }

        var selectedIds = _session.SelectedRegionEntityIds;
        var nextType = RegionEditing.CycleType(hit.Region.RegionType);
        var targets = selectedIds.Contains(hit.Region.EntityId)
            ? Map.Regions.Where(region => selectedIds.Contains(region.EntityId)).ToList()
            : [hit.Region];

        _session.Execute(
            targets.Count > 1 ? "Change region types" : "Change region type",
            () =>
            {
                foreach (var region in targets)
                {
                    RegionEditing.SetRegionType(region, nextType);
                }
            });

        if (selectedIds.Contains(hit.Region.EntityId) && selectedIds.Count > 1)
        {
            _session.SetRegionSelection(selectedIds, hit.Region.EntityId);
            return;
        }

        _session.RequestRegionTreeFocus(hit.Region);
    }

    public void HandlePrimaryClick(MapPoint previewPoint, bool shiftSelect = false)
    {
        switch (_session.ActiveMapTool)
        {
            case MapToolKind.Eraser:
                TryEraseAt(previewPoint);
                break;
            case MapToolKind.WallEditing:
                if (IsDrawingWall)
                {
                    // Left-click finish is handled on pointer pressed; ignore click fall-through.
                    break;
                }

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
                    EditWallAt(previewPoint, cycleType: true);
                }

                break;
            case MapToolKind.RegionEditing:
                if (IsDrawingRegion)
                {
                    break;
                }

                if (shiftSelect)
                {
                    HandleShiftSelectClick(previewPoint);
                    break;
                }

                if (!HasRegionAt(previewPoint))
                {
                    ClearRegionSelection();
                }
                else
                {
                    EditRegionAt(previewPoint, cycleType: true);
                }

                break;
        }
    }
}
