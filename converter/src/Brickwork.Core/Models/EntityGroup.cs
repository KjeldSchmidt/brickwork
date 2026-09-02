namespace Brickwork.Core.Models;

public sealed class EntityGroup
{
    public int GroupId { get; init; }

    public string? Name { get; set; }

    public string? LayerId { get; set; }

    public IList<int> MemberIds { get; init; } = [];

    /// <summary>Parent Inkarnate group entity id when this group is nested; null for root groups.</summary>
    public int? ParentGroupId { get; set; }

    public MapPoint Origin { get; set; }

    public MapPoint RotationPivot { get; set; }

    public double Angle { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"Group {GroupId}" : Name;
}
