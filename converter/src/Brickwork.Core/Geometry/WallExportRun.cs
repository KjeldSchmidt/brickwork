using Brickwork.Core.Models;

namespace Brickwork.Core.Geometry;

public sealed record WallExportRun(
    IReadOnlyList<MapPoint> Points,
    WallLineType LineType,
    bool IsPortal = false);
