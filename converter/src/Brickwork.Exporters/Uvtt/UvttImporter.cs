using System.Text.Json;
using Brickwork.Core.Models;
using Brickwork.Core.Ports;

namespace Brickwork.Exporters.Uvtt;

public sealed class UvttImporter : IMapImporter
{
    public string FormatId => "uvtt1";

    public static bool IsUvttPath(string path) =>
        IsUvttExtension(Path.GetExtension(path));

    private static bool IsUvttExtension(string extension) =>
        extension.Equals(".uvtt", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".dd2vtt", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".df2vtt", StringComparison.OrdinalIgnoreCase);

    public async Task<MapDocument> ImportAsync(Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var document = await JsonSerializer
            .DeserializeAsync<UvttDocument>(source, UvttJsonOptions.Create(), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("UVTT file did not contain a valid document.");

        return BuildMapDocument(document);
    }

    internal static MapDocument BuildMapDocument(UvttDocument document)
    {
        var origin = UvttCoordinateTransform.FromUvttPoint(document.Resolution.MapOrigin);
        const double cellSize = 1d;

        var columns = (int)Math.Round(document.Resolution.MapSize.X);
        var rows = (int)Math.Round(document.Resolution.MapSize.Y);
        var pixelsPerGrid = document.Resolution.PixelsPerGrid > 0
            ? document.Resolution.PixelsPerGrid
            : 70;

        byte[]? previewImagePng = null;
        PreviewDimensions? preview = null;

        if (!string.IsNullOrWhiteSpace(document.Image))
        {
            try
            {
                previewImagePng = Convert.FromBase64String(document.Image);
                preview = TryReadPngDimensions(previewImagePng);
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException("UVTT image field is not valid base64.", ex);
            }
        }

        preview ??= columns > 0 && rows > 0
            ? new PreviewDimensions
            {
                Width = columns * pixelsPerGrid,
                Height = rows * pixelsPerGrid,
            }
            : null;

        var map = new MapDocument
        {
            Name = "Imported UVTT Map",
            Scene = new SceneDimensions
            {
                Width = columns * cellSize,
                Height = rows * cellSize,
            },
            Preview = preview,
            PreviewImagePng = previewImagePng,
            Grid = new GridInfo
            {
                CellSize = cellSize,
                PixelsPerCell = pixelsPerGrid,
                Columns = columns,
                Rows = rows,
            },
        };

        var entityId = 1;

        foreach (var polyline in document.LineOfSight)
        {
            AddPolylineWall(map, polyline, UvttWallCategory.LineOfSight, origin, cellSize, ref entityId);
        }

        foreach (var polyline in document.ObjectsLineOfSight)
        {
            AddPolylineWall(map, polyline, UvttWallCategory.ObjectsLineOfSight, origin, cellSize, ref entityId);
        }

        foreach (var portal in document.Portals)
        {
            AddPortalWall(map, portal, origin, cellSize, ref entityId);
        }

        return map;
    }

    private static void AddPolylineWall(
        MapDocument map,
        IReadOnlyList<UvttPoint> polyline,
        UvttWallCategory category,
        MapPoint origin,
        double cellSize,
        ref int entityId)
    {
        if (polyline.Count < 2)
        {
            return;
        }

        var wall = new Wall
        {
            EntityId = entityId++,
            LineType = UvttWallMapping.ImportLineType(category),
            IsActive = true,
            WallEnabled = true,
        };

        foreach (var gridPoint in polyline)
        {
            wall.Points.Add(UvttCoordinateTransform.GridToScene(
                UvttCoordinateTransform.FromUvttPoint(gridPoint),
                origin,
                cellSize));
        }

        wall.IsClosed = UvttCoordinateTransform.IsClosedPolyline(wall.Points.ToList());
        map.Walls.Add(wall);
    }

    private static void AddPortalWall(
        MapDocument map,
        UvttPortal portal,
        MapPoint origin,
        double cellSize,
        ref int entityId)
    {
        if (portal.Bounds.Count < 2)
        {
            return;
        }

        var wall = new Wall
        {
            EntityId = entityId++,
            LineType = WallLineType.Door,
            IsActive = portal.Closed,
            WallEnabled = true,
        };

        foreach (var bound in portal.Bounds.Take(2))
        {
            wall.Points.Add(UvttCoordinateTransform.GridToScene(
                UvttCoordinateTransform.FromUvttPoint(bound),
                origin,
                cellSize));
        }

        map.Walls.Add(wall);
    }

    private static PreviewDimensions? TryReadPngDimensions(byte[] pngBytes)
    {
        if (pngBytes.Length < 24 ||
            pngBytes[0] != 0x89 ||
            pngBytes[1] != 0x50 ||
            pngBytes[2] != 0x4E ||
            pngBytes[3] != 0x47)
        {
            return null;
        }

        var width = (pngBytes[16] << 24) | (pngBytes[17] << 16) | (pngBytes[18] << 8) | pngBytes[19];
        var height = (pngBytes[20] << 24) | (pngBytes[21] << 16) | (pngBytes[22] << 8) | pngBytes[23];

        if (width <= 0 || height <= 0)
        {
            return null;
        }

        return new PreviewDimensions { Width = width, Height = height };
    }
}
