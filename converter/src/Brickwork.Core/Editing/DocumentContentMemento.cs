using Brickwork.Core.Models;

namespace Brickwork.Core.Editing;

/// <summary>
/// Deep snapshot of editable map content (layers + walls + groups) for undo/redo.
/// Restore updates existing instances in place so UI bindings keep working.
/// </summary>
public sealed class DocumentContentMemento
{
    private DocumentContentMemento(
        IReadOnlyList<MapLayer> layers,
        IReadOnlyList<Wall> walls,
        IReadOnlyList<EntityGroup> groups)
    {
        Layers = layers;
        Walls = walls;
        Groups = groups;
    }

    public IReadOnlyList<MapLayer> Layers { get; }

    public IReadOnlyList<Wall> Walls { get; }

    public IReadOnlyList<EntityGroup> Groups { get; }

    public static DocumentContentMemento Capture(MapDocument map) =>
        new(
            map.Layers.Select(CloneLayer).ToList(),
            map.Walls.Select(CloneWall).ToList(),
            map.Groups.Select(CloneGroup).ToList());

    public void RestoreTo(MapDocument map)
    {
        RestoreLayers(map);
        RestoreWalls(map);
        RestoreGroups(map);
    }

    public bool ContentEquals(DocumentContentMemento other)
    {
        if (Layers.Count != other.Layers.Count ||
            Walls.Count != other.Walls.Count ||
            Groups.Count != other.Groups.Count)
        {
            return false;
        }

        for (var i = 0; i < Layers.Count; i++)
        {
            if (!LayerEquals(Layers[i], other.Layers[i]))
            {
                return false;
            }
        }

        for (var i = 0; i < Walls.Count; i++)
        {
            if (!WallEquals(Walls[i], other.Walls[i]))
            {
                return false;
            }
        }

        for (var i = 0; i < Groups.Count; i++)
        {
            if (!GroupEquals(Groups[i], other.Groups[i]))
            {
                return false;
            }
        }

        return true;
    }

    private void RestoreLayers(MapDocument map)
    {
        var targetById = Layers.ToDictionary(
            layer => layer.Id,
            StringComparer.OrdinalIgnoreCase);
        for (var index = map.Layers.Count - 1; index >= 0; index--)
        {
            if (!targetById.ContainsKey(map.Layers[index].Id))
            {
                map.Layers.RemoveAt(index);
            }
        }

        var existingById = map.Layers.ToDictionary(
            layer => layer.Id,
            StringComparer.OrdinalIgnoreCase);
        foreach (var source in Layers)
        {
            if (existingById.TryGetValue(source.Id, out var existing))
            {
                CopyLayerInto(source, existing);
            }
            else
            {
                map.Layers.Add(CloneLayer(source));
            }
        }

        // Keep list order aligned with the snapshot.
        for (var index = 0; index < Layers.Count; index++)
        {
            var desiredId = Layers[index].Id;
            var currentIndex = -1;
            for (var search = 0; search < map.Layers.Count; search++)
            {
                if (string.Equals(map.Layers[search].Id, desiredId, StringComparison.OrdinalIgnoreCase))
                {
                    currentIndex = search;
                    break;
                }
            }

            if (currentIndex >= 0 && currentIndex != index)
            {
                var layer = map.Layers[currentIndex];
                map.Layers.RemoveAt(currentIndex);
                map.Layers.Insert(index, layer);
            }
        }
    }

    private void RestoreWalls(MapDocument map)
    {
        var targetById = Walls.ToDictionary(wall => wall.EntityId);
        for (var index = map.Walls.Count - 1; index >= 0; index--)
        {
            if (!targetById.ContainsKey(map.Walls[index].EntityId))
            {
                map.Walls.RemoveAt(index);
            }
        }

        var existingById = map.Walls.ToDictionary(wall => wall.EntityId);
        foreach (var source in Walls)
        {
            if (existingById.TryGetValue(source.EntityId, out var existing))
            {
                CopyWallInto(source, existing);
            }
            else
            {
                map.Walls.Add(CloneWall(source));
            }
        }
    }

    private void RestoreGroups(MapDocument map)
    {
        var targetById = Groups.ToDictionary(group => group.GroupId);
        for (var index = map.Groups.Count - 1; index >= 0; index--)
        {
            if (!targetById.ContainsKey(map.Groups[index].GroupId))
            {
                map.Groups.RemoveAt(index);
            }
        }

        var existingById = map.Groups.ToDictionary(group => group.GroupId);
        foreach (var source in Groups)
        {
            if (existingById.TryGetValue(source.GroupId, out var existing))
            {
                CopyGroupInto(source, existing);
            }
            else
            {
                map.Groups.Add(CloneGroup(source));
            }
        }
    }

    private static void CopyLayerInto(MapLayer source, MapLayer target)
    {
        target.Name = source.Name;
        target.Kind = source.Kind;
        target.IsVisible = source.IsVisible;
        target.Order = source.Order;
    }

    private static void CopyWallInto(Wall source, Wall target)
    {
        target.Name = source.Name;
        target.LayerId = source.LayerId;
        target.IsActive = source.IsActive;
        target.IsEntityVisible = source.IsEntityVisible;
        target.LineType = source.LineType;
        target.WallEnabled = source.WallEnabled;
        target.IsClosed = source.IsClosed;
        target.PathData = source.PathData;
        target.Origin = source.Origin;
        target.PathOrigin = source.PathOrigin;
        target.RotationPivot = source.RotationPivot;
        target.Angle = source.Angle;
        target.Scale = source.Scale;
        target.WallThickness = source.WallThickness;
        target.GroupId = source.GroupId;
        ReplacePoints(target.Points, source.Points);
        SyncPortals(source.Portals, target.Portals);
    }

    private static void SyncPortals(IList<WallPortal> source, IList<WallPortal> target)
    {
        var sourceById = source
            .Where(portal => !string.IsNullOrEmpty(portal.Id))
            .GroupBy(portal => portal.Id)
            .ToDictionary(group => group.Key, group => group.First());

        for (var index = target.Count - 1; index >= 0; index--)
        {
            var portal = target[index];
            if (string.IsNullOrEmpty(portal.Id) || !sourceById.ContainsKey(portal.Id))
            {
                target.RemoveAt(index);
            }
        }

        var targetById = target
            .Where(portal => !string.IsNullOrEmpty(portal.Id))
            .ToDictionary(portal => portal.Id);

        foreach (var sourcePortal in source)
        {
            if (!string.IsNullOrEmpty(sourcePortal.Id) &&
                targetById.TryGetValue(sourcePortal.Id, out var existing))
            {
                CopyPortalInto(sourcePortal, existing);
            }
            else
            {
                target.Add(ClonePortal(sourcePortal));
            }
        }
    }

    private static void CopyPortalInto(WallPortal source, WallPortal target)
    {
        target.Id = source.Id;
        target.Name = source.Name;
        target.Anchor = source.Anchor;
        target.Width = source.Width;
        target.IsActive = source.IsActive;
        target.LineType = source.LineType;
    }

    private static void CopyGroupInto(EntityGroup source, EntityGroup target)
    {
        target.Name = source.Name;
        target.LayerId = source.LayerId;
        target.ParentGroupId = source.ParentGroupId;
        target.Origin = source.Origin;
        target.RotationPivot = source.RotationPivot;
        target.Angle = source.Angle;
        target.MemberIds.Clear();
        foreach (var memberId in source.MemberIds)
        {
            target.MemberIds.Add(memberId);
        }
    }

    private static void ReplacePoints(IList<MapPoint> target, IEnumerable<MapPoint> source)
    {
        target.Clear();
        foreach (var point in source)
        {
            target.Add(point);
        }
    }

    private static MapLayer CloneLayer(MapLayer layer) =>
        new()
        {
            Id = layer.Id,
            Name = layer.Name,
            Kind = layer.Kind,
            IsVisible = layer.IsVisible,
            Order = layer.Order,
        };

    private static Wall CloneWall(Wall wall) =>
        new()
        {
            EntityId = wall.EntityId,
            Name = wall.Name,
            LayerId = wall.LayerId,
            IsActive = wall.IsActive,
            IsEntityVisible = wall.IsEntityVisible,
            LineType = wall.LineType,
            WallEnabled = wall.WallEnabled,
            IsClosed = wall.IsClosed,
            PathData = wall.PathData,
            Origin = wall.Origin,
            PathOrigin = wall.PathOrigin,
            RotationPivot = wall.RotationPivot,
            Angle = wall.Angle,
            Scale = wall.Scale,
            WallThickness = wall.WallThickness,
            GroupId = wall.GroupId,
            Points = wall.Points.ToList(),
            Portals = wall.Portals.Select(ClonePortal).ToList(),
        };

    private static WallPortal ClonePortal(WallPortal portal) =>
        new()
        {
            Id = portal.Id,
            Name = portal.Name,
            Anchor = portal.Anchor,
            Width = portal.Width,
            IsActive = portal.IsActive,
            LineType = portal.LineType,
        };

    private static EntityGroup CloneGroup(EntityGroup group) =>
        new()
        {
            GroupId = group.GroupId,
            Name = group.Name,
            LayerId = group.LayerId,
            MemberIds = group.MemberIds.ToList(),
            ParentGroupId = group.ParentGroupId,
            Origin = group.Origin,
            RotationPivot = group.RotationPivot,
            Angle = group.Angle,
        };

    private static bool LayerEquals(MapLayer a, MapLayer b) =>
        string.Equals(a.Id, b.Id, StringComparison.OrdinalIgnoreCase) &&
        a.Name == b.Name &&
        a.Kind == b.Kind &&
        a.IsVisible == b.IsVisible &&
        a.Order == b.Order;

    private static bool WallEquals(Wall a, Wall b) =>
        a.EntityId == b.EntityId &&
        a.Name == b.Name &&
        a.LayerId == b.LayerId &&
        a.IsActive == b.IsActive &&
        a.IsEntityVisible == b.IsEntityVisible &&
        a.LineType == b.LineType &&
        a.WallEnabled == b.WallEnabled &&
        a.IsClosed == b.IsClosed &&
        a.PathData == b.PathData &&
        a.Origin.Equals(b.Origin) &&
        a.PathOrigin.Equals(b.PathOrigin) &&
        a.RotationPivot.Equals(b.RotationPivot) &&
        a.Angle.Equals(b.Angle) &&
        a.Scale.Equals(b.Scale) &&
        a.WallThickness.Equals(b.WallThickness) &&
        a.GroupId == b.GroupId &&
        a.Points.SequenceEqual(b.Points) &&
        PortalListsEqual(a.Portals, b.Portals);

    private static bool PortalListsEqual(IList<WallPortal> a, IList<WallPortal> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (!PortalEquals(a[i], b[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool PortalEquals(WallPortal a, WallPortal b) =>
        a.Id == b.Id &&
        a.Name == b.Name &&
        a.Anchor.Equals(b.Anchor) &&
        a.Width.Equals(b.Width) &&
        a.IsActive == b.IsActive &&
        a.LineType == b.LineType;

    private static bool GroupEquals(EntityGroup a, EntityGroup b) =>
        a.GroupId == b.GroupId &&
        a.Name == b.Name &&
        a.LayerId == b.LayerId &&
        a.ParentGroupId == b.ParentGroupId &&
        a.Origin.Equals(b.Origin) &&
        a.RotationPivot.Equals(b.RotationPivot) &&
        a.Angle.Equals(b.Angle) &&
        a.MemberIds.SequenceEqual(b.MemberIds);
}
