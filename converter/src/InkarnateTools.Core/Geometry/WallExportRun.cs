using InkarnateTools.Core.Models;

namespace InkarnateTools.Core.Geometry;

public sealed record WallExportRun(
    IReadOnlyList<MapPoint> Points,
    WallLineType LineType,
    bool IsPortal = false);
