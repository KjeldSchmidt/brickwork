using Brickwork.Core.Geometry;
using Brickwork.Core.Models;
using Xunit;

namespace Brickwork.Core.Tests;

public class LightHandleTests
{
    [Fact]
    public void HandlePositions_ScaleWithCellSize_OnHorizontalAxis()
    {
        var light = new LightSource
        {
            Position = new MapPoint(100, 200),
            BrightRadius = 2,
            DimRadius = 5,
        };

        var bright = LightRadiusHandleGeometry.BrightHandleScene(light, cellSize: 10);
        var dim = LightRadiusHandleGeometry.DimHandleScene(light, cellSize: 10);

        Assert.Equal(new MapPoint(120, 200), bright);
        Assert.Equal(new MapPoint(50, 200), dim);
    }

    [Fact]
    public void SetBrightRadius_RaisesDimWhenNeeded()
    {
        var light = new LightSource
        {
            Position = new MapPoint(0, 0),
            BrightRadius = 2,
            DimRadius = 3,
        };

        LightRadiusHandleGeometry.SetRadiusFromScene(
            light,
            cellSize: 10,
            scenePoint: new MapPoint(50, 0),
            LightRadiusHandleKind.Bright);

        Assert.Equal(5, light.BrightRadius, precision: 6);
        Assert.Equal(5, light.DimRadius, precision: 6);
    }

    [Fact]
    public void SetDimRadius_ClampsToBright()
    {
        var light = new LightSource
        {
            Position = new MapPoint(0, 0),
            BrightRadius = 4,
            DimRadius = 8,
        };

        LightRadiusHandleGeometry.SetDimRadiusFromScene(
            light,
            cellSize: 10,
            scenePoint: new MapPoint(10, 0));

        Assert.Equal(4, light.BrightRadius, precision: 6);
        Assert.Equal(4, light.DimRadius, precision: 6);
    }

    [Fact]
    public void SetRadiusSquares_AppliesAbsoluteValueWithClamp()
    {
        var a = new LightSource { BrightRadius = 1, DimRadius = 2 };
        var b = new LightSource { BrightRadius = 3, DimRadius = 9 };

        LightRadiusHandleGeometry.SetRadiusSquares(a, 5, LightRadiusHandleKind.Bright);
        LightRadiusHandleGeometry.SetRadiusSquares(b, 5, LightRadiusHandleKind.Bright);

        Assert.Equal(5, a.BrightRadius);
        Assert.Equal(5, a.DimRadius);
        Assert.Equal(5, b.BrightRadius);
        Assert.Equal(9, b.DimRadius);
    }

    [Fact]
    public void HandleHitTester_PrefersOriginOverRings()
    {
        var map = CreateMap(
            width: 1000,
            cellSize: 100,
            new LightSource
            {
                EntityId = 1,
                Position = new MapPoint(500, 500),
                BrightRadius = 1,
                DimRadius = 1,
                IsActive = true,
            });

        var transform = SceneTransform.ForMapPreview(map);
        Assert.NotNull(transform);
        var originPreview = transform!.SceneToPreview(map.Lights[0].Position);

        var hit = LightHandleHitTester.Pick(map, originPreview, tolerancePreviewPixels: 8);

        Assert.NotNull(hit);
        Assert.Equal(LightHandleKind.Origin, hit!.Kind);
    }

    [Fact]
    public void HandleHitTester_PrefersBrightRingOverDimWhenNearlyEqual()
    {
        var map = CreateMap(
            width: 1000,
            cellSize: 100,
            new LightSource
            {
                EntityId = 1,
                Position = new MapPoint(500, 500),
                BrightRadius = 2,
                DimRadius = 2.1,
                IsActive = true,
            });

        var transform = SceneTransform.ForMapPreview(map);
        Assert.NotNull(transform);
        var center = transform!.SceneToPreview(map.Lights[0].Position);
        var brightRadiusPreview = map.Lights[0].BrightRadius * map.Grid.CellSize * transform.ScaleX;
        // Pick the top of the ring so the +X bright side-handle is not competing.
        var onBrightRing = new MapPoint(center.X, center.Y - brightRadiusPreview);

        var hit = LightHandleHitTester.Pick(map, onBrightRing, tolerancePreviewPixels: 8);

        Assert.NotNull(hit);
        Assert.Equal(LightHandleKind.BrightRing, hit!.Kind);
    }

    [Fact]
    public void HitTester_FindsLightByOrigin()
    {
        var map = CreateMap(
            width: 500,
            cellSize: 50,
            new LightSource { EntityId = 7, Position = new MapPoint(100, 100) });

        var transform = SceneTransform.ForMapPreview(map);
        Assert.NotNull(transform);
        var preview = transform!.SceneToPreview(new MapPoint(100, 100));

        var hit = LightHitTester.Pick(map, preview, tolerancePreviewPixels: 8, includeRings: false);

        Assert.NotNull(hit);
        Assert.Equal(7, hit!.Light.EntityId);
    }

    private static MapDocument CreateMap(int width, double cellSize, params LightSource[] lights) =>
        new()
        {
            Preview = new PreviewDimensions { Width = width, Height = width },
            Scene = new SceneDimensions { Width = width, Height = width },
            Grid = new GridInfo { CellSize = cellSize },
            Lights = lights.ToList(),
        };
}
