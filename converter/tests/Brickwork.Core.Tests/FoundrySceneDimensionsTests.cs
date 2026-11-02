using Brickwork.Exporters.Foundry;
using Xunit;

namespace Brickwork.Core.Tests;

public class FoundrySceneDimensionsTests
{
    [Theory]
    [InlineData(4000, 3000, 0.25, 100, 0, 0, 1000, 800)]
    [InlineData(3880, 2800, 0.5, 70, 0, 0, 1960, 1400)]
    [InlineData(3555, 3555, 0, 100, 0, 0, 0, 0)]
    [InlineData(1000, 1000, 0.25, 100, 50, 25, 250, 275)]
    public void ComputeOrigin_MatchesFoundrySquareGridPadding(
        double width,
        double height,
        double padding,
        int gridSize,
        double shiftX,
        double shiftY,
        double expectedX,
        double expectedY)
    {
        var origin = FoundrySceneDimensions.ComputeOrigin(width, height, padding, gridSize, shiftX, shiftY);

        Assert.Equal(expectedX, origin.SceneX);
        Assert.Equal(expectedY, origin.SceneY);
    }
}
