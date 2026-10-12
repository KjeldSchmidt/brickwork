using Brickwork.Core.Models;
using Brickwork.Inkarnate.Parsing;
using System.Text.Json;
using Xunit;

namespace Brickwork.Core.Tests;

public class SceneTransformTests
{
    [Fact]
    public void FromMap_ReturnsNull_WhenPreviewMissing()
    {
        var map = new MapDocument
        {
            Scene = new SceneDimensions { Width = 8192, Height = 6144 },
        };

        Assert.Null(SceneTransform.FromMap(map));
    }

    [Fact]
    public void SceneToPreview_ScalesCoordinates()
    {
        var transform = new SceneTransform
        {
            SceneWidth = 8192,
            SceneHeight = 6144,
            PreviewWidth = 2048,
            PreviewHeight = 1536,
        };

        var previewPoint = transform.SceneToPreview(new MapPoint(8192, 6144));

        Assert.Equal(2048, previewPoint.X, precision: 3);
        Assert.Equal(1536, previewPoint.Y, precision: 3);
    }

    [Fact]
    public void PreviewToScene_InvertsScale()
    {
        var transform = new SceneTransform
        {
            SceneWidth = 8192,
            SceneHeight = 6144,
            PreviewWidth = 2048,
            PreviewHeight = 1536,
        };

        var scenePoint = transform.PreviewToScene(new MapPoint(1024, 768));

        Assert.Equal(4096, scenePoint.X, precision: 3);
        Assert.Equal(3072, scenePoint.Y, precision: 3);
    }

    [Fact]
    public void SceneToPreview_IncludesOriginOffset()
    {
        var transform = new SceneTransform
        {
            SceneWidth = 100,
            SceneHeight = 100,
            PreviewWidth = 100,
            PreviewHeight = 100,
            OriginOffsetX = 40,
            OriginOffsetY = 20,
        };

        var previewPoint = transform.SceneToPreview(new MapPoint(0, 0));

        Assert.Equal(40, previewPoint.X, precision: 3);
        Assert.Equal(20, previewPoint.Y, precision: 3);
    }

    [Fact]
    public void ForMapPreview_PadsForOutOfBoundsWalls()
    {
        var map = new MapDocument
        {
            Scene = new SceneDimensions { Width = 100, Height = 100 },
            Preview = new PreviewDimensions { Width = 100, Height = 100 },
            Walls =
            [
                new Wall
                {
                    Points =
                    [
                        new MapPoint(-10, 10),
                        new MapPoint(110, 10),
                    ],
                },
            ],
        };

        var transform = SceneTransform.ForMapPreview(map);

        Assert.NotNull(transform);
        Assert.True(transform.OriginOffsetX >= 10 + SceneTransform.OverflowMargin);
        Assert.True(transform.RightPadding >= 10 + SceneTransform.OverflowMargin);
        Assert.True(transform.CanvasWidth > transform.PreviewWidth);
        Assert.Equal(0, transform.SceneToPreview(new MapPoint(0, 0)).X - transform.OriginOffsetX, precision: 3);
    }
}

public class InkPreviewImageReaderTests
{
    [Fact]
    public void ReadPreviewImagePng_DecodesDataUrl()
    {
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var base64 = Convert.ToBase64String(pngBytes);
        using var document = JsonDocument.Parse($$"""{"preview":"data:image/png;base64,{{base64}}"}""");

        var result = InkPreviewImageReader.ReadPreviewImagePng(document.RootElement);

        Assert.Equal(pngBytes, result);
    }

    [Fact]
    public void ReadPreviewImagePng_ReturnsNull_ForMissingField()
    {
        using var document = JsonDocument.Parse("""{"title":"no preview"}""");

        Assert.Null(InkPreviewImageReader.ReadPreviewImagePng(document.RootElement));
    }
}
