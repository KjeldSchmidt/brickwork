using Brickwork.Core.Geometry;
using Brickwork.Core.Models;
using Xunit;

namespace Brickwork.Core.Tests;

public class WallOffsetTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Apply_ClosedSquare_PositiveGrowsOutwardRegardlessOfWinding(bool reversed)
    {
        var points = new List<MapPoint>
        {
            new(0, 0),
            new(100, 0),
            new(100, 100),
            new(0, 100),
        };
        if (reversed)
        {
            points.Reverse();
        }

        var wall = new Wall { IsClosed = true, Points = points };

        WallOffset.Apply(wall, 10);

        Assert.Equal(4, wall.Points.Count);
        Assert.Contains(new MapPoint(-10, -10), wall.Points, new PointComparer());
        Assert.Contains(new MapPoint(110, 110), wall.Points, new PointComparer());
    }

    [Fact]
    public void Apply_ClosedSquare_NegativeShrinksInward()
    {
        var wall = new Wall
        {
            IsClosed = true,
            Points = [new(0, 0), new(100, 0), new(100, 100), new(0, 100)],
        };

        WallOffset.Apply(wall, -10);

        Assert.Contains(new MapPoint(10, 10), wall.Points, new PointComparer());
        Assert.Contains(new MapPoint(90, 90), wall.Points, new PointComparer());
    }

    [Fact]
    public void Apply_OpenLine_PositiveMovesToScreenRightOfDirection()
    {
        // Travelling +X in y-down scene space: screen-right is +Y.
        var wall = new Wall { Points = [new(0, 0), new(100, 0)] };

        WallOffset.Apply(wall, 5);

        Assert.Equal(new MapPoint(0, 5), wall.Points[0], new PointComparer());
        Assert.Equal(new MapPoint(100, 5), wall.Points[1], new PointComparer());
    }

    [Fact]
    public void Apply_SharpSpike_BevelsInsteadOfLongMiter()
    {
        var wall = new Wall { Points = [new(0, 0), new(100, 1), new(0, 2)] };

        WallOffset.Apply(wall, -5);

        Assert.Equal(4, wall.Points.Count);
        Assert.All(wall.Points, point => Assert.True(point.X < 110));
    }

    [Fact]
    public void Apply_KeepsPortalOnOffsetWall()
    {
        var wall = new Wall
        {
            Points = [new(0, 0), new(100, 0)],
            Portals = [new WallPortal { Anchor = new MapPoint(50, 0), Width = 10 }],
        };

        WallOffset.Apply(wall, 20);

        var anchor = WallPathSegmentBuilder.PortalAnchorToScene(wall, wall.Portals[0]);
        Assert.Equal(new MapPoint(50, 20), anchor, new PointComparer());
    }

    [Fact]
    public void Apply_IsAbsolute_NotCumulative()
    {
        var wall = new Wall { Points = [new(0, 0), new(100, 0)] };

        WallOffset.Apply(wall, 10);
        WallOffset.Apply(wall, 5);

        Assert.Equal(5, wall.Offset);
        Assert.Equal(new MapPoint(0, 5), wall.Points[0], new PointComparer());
    }

    [Fact]
    public void Apply_Zero_RestoresOriginalPath()
    {
        var original = new List<MapPoint> { new(0, 0), new(100, 0), new(100, 100), new(0, 100) };
        var wall = new Wall { IsClosed = true, Points = original.ToList() };

        WallOffset.Apply(wall, 25);
        WallOffset.Apply(wall, -7);
        WallOffset.Apply(wall, 0);

        Assert.Equal(original, wall.Points);
        Assert.Equal(0, wall.Offset);
        Assert.Empty(wall.OffsetBasePoints);
    }

    [Fact]
    public void Apply_AfterTranslate_KeepsOffsetRelativeToMovedPath()
    {
        var wall = new Wall { Points = [new(0, 0), new(100, 0)] };

        WallOffset.Apply(wall, 10);
        WallGeometryEditing.Translate(wall, 0, 50);
        WallOffset.Apply(wall, 5);

        Assert.Equal(new MapPoint(0, 55), wall.Points[0], new PointComparer());
    }

    [Fact]
    public void Apply_AfterVertexEdit_KeepsEditAsNewOriginalPath()
    {
        var wall = new Wall { Points = [new(0, 0), new(100, 0)] };

        WallOffset.Apply(wall, 10);
        WallGeometryEditing.SetVertexPosition(wall, 1, new MapPoint(200, 10));
        WallOffset.Apply(wall, 5);

        // The edited, already-offset line (0,10)-(200,10) is the new base, moved down by 5.
        Assert.Equal(new MapPoint(0, 15), wall.Points[0], new PointComparer());
        Assert.Equal(new MapPoint(200, 15), wall.Points[1], new PointComparer());
    }

    private sealed class PointComparer : IEqualityComparer<MapPoint>
    {
        public bool Equals(MapPoint a, MapPoint b) =>
            Math.Abs(a.X - b.X) < 1e-6 && Math.Abs(a.Y - b.Y) < 1e-6;

        public int GetHashCode(MapPoint point) => 0;
    }
}
