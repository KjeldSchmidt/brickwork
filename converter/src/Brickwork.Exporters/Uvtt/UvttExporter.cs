using System.Text.Json;
using Brickwork.Core.Geometry;
using Brickwork.Core.Models;
using Brickwork.Core.Ports;

namespace Brickwork.Exporters.Uvtt;

public sealed class UvttExporter : IMapExporter
{
    public string FormatId => "uvtt1";

    public Task ExportAsync(MapDocument map, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(destination);

        if (map.PreviewImagePng is not { Length: > 0 })
        {
            throw new InvalidOperationException(
                "UVTT export requires an embedded map image (PreviewImagePng).");
        }

        var document = BuildDocument(map);
        return JsonSerializer.SerializeAsync(destination, document, UvttJsonOptions.Create(), cancellationToken);
    }

    internal static UvttDocument BuildDocument(MapDocument map)
    {
        var columns = map.Grid.Columns > 0
            ? map.Grid.Columns
            : (int)Math.Round(map.Scene.Width / Math.Max(map.Grid.CellSize, 1d));
        var rows = map.Grid.Rows > 0
            ? map.Grid.Rows
            : (int)Math.Round(map.Scene.Height / Math.Max(map.Grid.CellSize, 1d));
        var pixelsPerGrid = map.Grid.PixelsPerCell > 0 ? map.Grid.PixelsPerCell : 70;
        var cellSize = map.Grid.CellSize > 0 ? map.Grid.CellSize : 1d;
        var origin = new MapPoint(0, 0);

        var lineOfSight = new List<List<UvttPoint>>();
        var objectsLineOfSight = new List<List<UvttPoint>>();
        var portals = new List<UvttPortal>();

        foreach (var wall in map.Walls.Where(w => w.WallEnabled))
        {
            foreach (var run in WallPathSegmentBuilder.BuildExportRuns(wall))
            {
                if (!wall.IsActive && !run.IsPortal)
                {
                    continue;
                }

                if (run.Points.Count < 2)
                {
                    continue;
                }

                var gridPoints = run.Points
                    .Select(point => UvttCoordinateTransform.SceneToGrid(point, origin, cellSize))
                    .Select(UvttCoordinateTransform.ToUvttPoint)
                    .ToList();

                switch (UvttWallMapping.ExportCategory(run.LineType))
                {
                    case UvttWallCategory.Portal:
                        portals.Add(CreatePortal(gridPoints));
                        break;
                    case UvttWallCategory.ObjectsLineOfSight:
                        objectsLineOfSight.Add(gridPoints);
                        break;
                    default:
                        lineOfSight.Add(gridPoints);
                        break;
                }
            }
        }

        return new UvttDocument
        {
            Software = "Brickwork",
            Format = 1.0,
            Resolution = new UvttResolution
            {
                MapOrigin = new UvttPoint(),
                MapSize = new UvttPoint { X = columns, Y = rows },
                PixelsPerGrid = pixelsPerGrid,
            },
            LineOfSight = lineOfSight,
            ObjectsLineOfSight = objectsLineOfSight,
            Portals = portals,
            Environment = new UvttEnvironment { BakedLighting = false },
            Lights = [],
            Image = Convert.ToBase64String(map.PreviewImagePng!),
        };
    }

    private static UvttPortal CreatePortal(IReadOnlyList<UvttPoint> gridPoints)
    {
        var start = gridPoints[0];
        var end = gridPoints[^1];
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;

        return new UvttPortal
        {
            Position = new UvttPoint
            {
                X = (start.X + end.X) / 2d,
                Y = (start.Y + end.Y) / 2d,
            },
            Bounds = [start, end],
            Rotation = Math.Atan2(dy, dx),
            Closed = true,
            Freestanding = false,
        };
    }
}
