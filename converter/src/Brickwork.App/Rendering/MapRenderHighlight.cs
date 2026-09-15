using Brickwork.Core.Models;

namespace Brickwork.App.Rendering;

public sealed record MapRenderHighlight(
    IReadOnlySet<int> SelectedWallEntityIds,
    int? FocusedWallEntityId,
    WallPortal? FocusedPortal,
    int? HoveredWallEntityId,
    WallPortal? HoveredPortal)
{
    public static MapRenderHighlight Empty { get; } = new(
        new HashSet<int>(),
        null,
        null,
        null,
        null);

    public WallHighlightTarget? HoverTarget
    {
        get
        {
            if (HoveredWallEntityId is int hoveredId)
            {
                return new WallHighlightTarget(hoveredId, HoveredPortal);
            }

            return null;
        }
    }
}

public readonly record struct WallHighlightTarget(int WallEntityId, WallPortal? Portal);
