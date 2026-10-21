using Brickwork.Core.Export;
using Brickwork.Core.Models;
using Xunit;

namespace Brickwork.Core.Tests;

public class FoundryExportCanvasTests
{
    [Fact]
    public void FromMap_ConnectedUnderlyingMap_UsesImagePixelsAndDerivedGridSize()
    {
        var map = CreateMap(columns: 40, rows: 30, previewWidth: 2048, previewHeight: 1536);
        map.UnderlyingMap = new UnderlyingMapInfo
        {
            FileName = "dungeon.webp",
            AbsolutePath = @"C:\maps\dungeon.webp",
            Width = 4800,
            Height = 3600,
        };

        var canvas = FoundryExportCanvas.FromMap(map);

        Assert.Equal(4800, canvas.Width);
        Assert.Equal(3600, canvas.Height);
        Assert.Equal(120, canvas.GridSize);
        Assert.Equal(4800, canvas.Transform.PreviewWidth);
        Assert.Equal(3600, canvas.Transform.PreviewHeight);

        var point = canvas.Transform.SceneToPreview(new MapPoint(map.Scene.Width, map.Scene.Height));
        Assert.Equal(4800, point.X, precision: 6);
        Assert.Equal(3600, point.Y, precision: 6);
    }

    [Fact]
    public void FromMap_DisconnectedWithGrid_DefaultsTo150PixelsPerSquare()
    {
        var map = CreateMap(columns: 40, rows: 30, previewWidth: 2048, previewHeight: 1536);

        var canvas = FoundryExportCanvas.FromMap(map);

        Assert.Equal(6000, canvas.Width);
        Assert.Equal(4500, canvas.Height);
        Assert.Equal(150, canvas.GridSize);
    }

    [Fact]
    public void FromMap_DisconnectedWithGrid_UsesConfiguredGridSize()
    {
        var map = CreateMap(columns: 40, rows: 30, previewWidth: 2048, previewHeight: 1536);
        map.FoundryGridSize = 100;

        var canvas = FoundryExportCanvas.FromMap(map);

        Assert.Equal(4000, canvas.Width);
        Assert.Equal(3000, canvas.Height);
        Assert.Equal(100, canvas.GridSize);

        var point = canvas.Transform.SceneToPreview(new MapPoint(map.Scene.Width / 2, map.Scene.Height / 2));
        Assert.Equal(2000, point.X, precision: 6);
        Assert.Equal(1500, point.Y, precision: 6);
    }

    [Fact]
    public void FromMap_WithoutGrid_FallsBackToPreview()
    {
        var map = CreateMap(columns: 0, rows: 0, previewWidth: 2048, previewHeight: 1536);

        var canvas = FoundryExportCanvas.FromMap(map);

        Assert.Equal(2048, canvas.Width);
        Assert.Equal(1536, canvas.Height);
        Assert.Null(canvas.GridSize);
    }

    [Fact]
    public void FromMap_ConnectedWithoutGrid_FallsBackToPreview()
    {
        var map = CreateMap(columns: 0, rows: 0, previewWidth: 800, previewHeight: 600);
        map.UnderlyingMap = new UnderlyingMapInfo
        {
            FileName = "dungeon.webp",
            AbsolutePath = @"C:\maps\dungeon.webp",
            Width = 4800,
            Height = 3600,
        };

        var canvas = FoundryExportCanvas.FromMap(map);

        Assert.Equal(800, canvas.Width);
        Assert.Equal(600, canvas.Height);
        Assert.Null(canvas.GridSize);
    }

    [Fact]
    public void FromMap_ExportGrid_OverridesImageAndScalesSquares()
    {
        var map = CreateMap(columns: 40, rows: 30, previewWidth: 2048, previewHeight: 1536);
        map.UnderlyingMap = new UnderlyingMapInfo
        {
            FileName = "dungeon.webp",
            AbsolutePath = @"C:\maps\dungeon.webp",
            Width = 12000,
            Height = 9000,
        };
        map.FoundryExportGrid = new FoundryExportGrid(120, 90, 150);

        var canvas = FoundryExportCanvas.FromMap(map);

        Assert.Equal(18000, canvas.Width);
        Assert.Equal(13500, canvas.Height);
        Assert.Equal(150, canvas.GridSize);
        Assert.Equal(3, canvas.SquaresPerMapCell, precision: 9);

        var corner = canvas.Transform.SceneToPreview(new MapPoint(map.Scene.Width, map.Scene.Height));
        Assert.Equal(18000, corner.X, precision: 6);
        Assert.Equal(13500, corner.Y, precision: 6);
    }

    private static MapDocument CreateMap(
        int columns,
        int rows,
        int previewWidth,
        int previewHeight) =>
        new()
        {
            Scene = new SceneDimensions { Width = 8192, Height = 6144 },
            Preview = new PreviewDimensions { Width = previewWidth, Height = previewHeight },
            Grid = new GridInfo
            {
                Columns = columns,
                Rows = rows,
                CellSize = columns > 0 ? 8192d / columns : 0,
            },
        };
}
