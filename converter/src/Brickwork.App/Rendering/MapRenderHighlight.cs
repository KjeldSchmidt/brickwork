using Brickwork.Core.Models;

namespace Brickwork.App.Rendering;

public sealed record MapRenderHighlight(
    IReadOnlySet<int> SelectedWallEntityIds,
    int? FocusedWallEntityId,
    WallPortal? FocusedPortal,
    int? HoveredWallEntityId,
    WallPortal? HoveredPortal,
    IReadOnlySet<int> SelectedRegionEntityIds,
    int? FocusedRegionEntityId,
    int? HoveredRegionEntityId,
    bool ShowWallHandles = false,
    bool ShowRegionHandles = false,
    IReadOnlySet<int>? SelectedLightEntityIds = null,
    int? FocusedLightEntityId = null,
    int? HoveredLightEntityId = null,
    bool ShowLightRadii = true,
    bool ShowLightHandles = false)
{
    public static MapRenderHighlight Empty { get; } = new(
        new HashSet<int>(),
        null,
        null,
        null,
        null,
        new HashSet<int>(),
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

    public int? RegionHoverTarget => HoveredRegionEntityId;

    public IReadOnlySet<int> LightSelection => SelectedLightEntityIds ?? (IReadOnlySet<int>)new HashSet<int>();
}

public readonly record struct WallHighlightTarget(int WallEntityId, WallPortal? Portal);
