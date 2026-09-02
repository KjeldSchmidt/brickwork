using Brickwork.Core.Models;

namespace Brickwork.Core.Geometry;

public sealed record WallTerrainRing(
    IReadOnlyList<MapPoint> Outer,
    IReadOnlyList<MapPoint> Inner);
