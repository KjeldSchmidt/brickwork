using Brickwork.Core.Geometry;
using Xunit;

namespace Brickwork.Core.Tests;

public class WallCircularIntervalsTests
{
    [Fact]
    public void ExpandPortalGap_OpenPath_ClampsToEnds()
    {
        var gaps = WallCircularIntervals.ExpandPortalGap(50, 10, 100, isClosed: false);

        Assert.Single(gaps);
        Assert.Equal((40d, 60d), gaps[0]);
    }

    [Fact]
    public void ExpandPortalGap_ClosedPathCrossingSeam_SplitsIntoTwoIntervals()
    {
        var gaps = WallCircularIntervals.ExpandPortalGap(0, 15, 400, isClosed: true);

        Assert.Equal(2, gaps.Count);
        Assert.Equal((385d, 400d), gaps[0]);
        Assert.Equal((0d, 15d), gaps[1]);
    }

    [Fact]
    public void NormalizeArcLength_WrapsNegativeAndOverflow()
    {
        Assert.Equal(385d, WallCircularIntervals.NormalizeArcLength(-15, 400), precision: 3);
        Assert.Equal(15d, WallCircularIntervals.NormalizeArcLength(415, 400), precision: 3);
    }

    [Fact]
    public void ForwardArcDistance_ClosedPath_UsesShortestForwardDirection()
    {
        Assert.Equal(15d, WallCircularIntervals.ForwardArcDistance(0, 15, 400), precision: 3);
        Assert.Equal(15d, WallCircularIntervals.ForwardArcDistance(385, 0, 400), precision: 3);
    }

    [Fact]
    public void MaxPortalHalfWidth_ClosedPath_AllowsHalfPerimeter()
    {
        Assert.Equal(200d, WallCircularIntervals.MaxPortalHalfWidth(5, 400, isClosed: true), precision: 3);
    }
}
