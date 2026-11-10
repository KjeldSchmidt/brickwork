using Brickwork.Core.Models;

namespace Brickwork.Inkarnate.Parsing;

/// <summary>Import-time state of an Inkarnate stamp; only kept to detect light sources.</summary>
internal sealed class InkStamp
{
    public int EntityId { get; init; }

    /// <summary>User-assigned name, if the stamp was renamed in Inkarnate.</summary>
    public string? Name { get; set; }

    /// <summary>Asset name of the stamp (e.g. <c>Hand Lantern Lit</c>).</summary>
    public string? DefaultName { get; set; }

    public string? LayerId { get; set; }

    public int? GroupId { get; set; }

    /// <summary>Scene-space centre of the stamp.</summary>
    public MapPoint Position { get; set; }

    public bool IsEntityVisible { get; set; } = true;
}
