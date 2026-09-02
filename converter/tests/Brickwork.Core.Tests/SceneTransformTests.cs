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
