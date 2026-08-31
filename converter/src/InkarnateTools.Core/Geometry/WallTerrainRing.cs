using InkarnateTools.Core.Models;

namespace InkarnateTools.Core.Geometry;

public sealed record WallTerrainRing(
    IReadOnlyList<MapPoint> Outer,
    IReadOnlyList<MapPoint> Inner);
