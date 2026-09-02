using Brickwork.Core.Models;

namespace Brickwork.App.Rendering;

public sealed record MapRenderHighlight(
    int? FocusedWallEntityId,
    WallPortal? FocusedPortal,
    int? HoveredWallEntityId,
    WallPortal? HoveredPortal)
{
    /// <summary>
    /// The wall/portals to draw with highlight styling. Hover takes precedence over focus.
    /// </summary>
    public WallHighlightTarget? ActiveTarget
    {
        get
        {
            if (HoveredWallEntityId is int hoveredId)
            {
                return new WallHighlightTarget(hoveredId, HoveredPortal);
            }

            if (FocusedWallEntityId is int focusedId)
            {
                return new WallHighlightTarget(focusedId, FocusedPortal);
            }

            return null;
        }
    }
}

public readonly record struct WallHighlightTarget(int WallEntityId, WallPortal? Portal);
