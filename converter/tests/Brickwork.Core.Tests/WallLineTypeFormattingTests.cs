using Brickwork.Core.Models;
using Xunit;

namespace Brickwork.Core.Tests;

public class WallLineTypeFormattingTests
{
    [Fact]
    public void ToDisplayName_SecretDoor_UsesSpacedLabel() =>
        Assert.Equal("Secret Door", WallLineType.SecretDoor.ToDisplayName());

    [Theory]
    [InlineData(WallLineType.Solid, "Solid")]
    [InlineData(WallLineType.Terrain, "Terrain")]
    [InlineData(WallLineType.Invisible, "Invisible")]
    [InlineData(WallLineType.Ethereal, "Ethereal")]
    [InlineData(WallLineType.Door, "Door")]
    [InlineData(WallLineType.Window, "Window")]
    public void ToDisplayName_OtherTypes_UseEnumName(WallLineType lineType, string expected) =>
        Assert.Equal(expected, lineType.ToDisplayName());
}
