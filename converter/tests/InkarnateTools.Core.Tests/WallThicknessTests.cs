using InkarnateTools.Core.Models;
using Xunit;

namespace InkarnateTools.Core.Tests;

public class WallThicknessTests
{
    [Fact]
    public void SceneThickness_ScalesByWallScale()
    {
        var wall = new Wall
        {
            WallThickness = 100,
            Scale = 2,
        };

        Assert.Equal(200, wall.SceneThickness, precision: 6);
    }
}
