using System.Text.Json.Serialization;

namespace Brickwork.Core.Models;

/// <summary>
/// How one wall of a tree trunk X is built, so it can be rebuilt exactly when the tree's version
/// is adjusted: the X is centred on the stamp (plus the version's offset, turned with the stamp),
/// rotated like the stamp, and sized from the import setting times the version's scale.
/// </summary>
/// <param name="StampId">Entity id of the tree stamp.</param>
/// <param name="AssetId">Inkarnate image (asset) id of the stamp; trees sharing it are one version.</param>
/// <param name="Arm">0 or 1: which of the two crossing walls this is.</param>
/// <param name="StampCenter">Scene-space centre of the stamp.</param>
/// <param name="Angle">Stamp rotation in degrees.</param>
/// <param name="Unit">Scene length of one grid square in the tree image's frame (cell size times the stamp's size ratio); used for offsets.</param>
/// <param name="SizeUnit">Scene length of one grid square of trunk size (cell size times the damped size ratio; see <see cref="TreeTrunks.SizeFactor"/>).</param>
/// <param name="BaseSize">Trunk size from the import settings, in grid squares.</param>
public sealed record TreeTrunk(
    int StampId,
    int? AssetId,
    int Arm,
    MapPoint StampCenter,
    double Angle,
    double Unit,
    double SizeUnit,
    double BaseSize)
{
    /// <summary>Version scale factor (1 = <see cref="BaseSize"/>).</summary>
    public double Scale { get; init; } = 1;

    /// <summary>Version offset to the right, in grid squares, in the stamp's own (unrotated) frame.</summary>
    public double OffsetX { get; init; }

    /// <summary>Version offset downwards, in grid squares, in the stamp's own (unrotated) frame.</summary>
    public double OffsetY { get; init; }
}

/// <summary>Per-version trunk adjustment, remembered across maps in the tree-trunk library.</summary>
public sealed class TreeTrunkAdjustment
{
    /// <summary>Tree name, for people reading the library file.</summary>
    public string? Name { get; set; }

    public double Scale { get; set; } = 1;

    public double OffsetX { get; set; }

    public double OffsetY { get; set; }

    /// <summary>Scale 1 and no offset, i.e. the trunk as imported.</summary>
    [JsonIgnore]
    public bool IsIdentity => Math.Abs(Scale - 1) < 1e-9 && Math.Abs(OffsetX) < 1e-9 && Math.Abs(OffsetY) < 1e-9;

    /// <summary>Same scale and offset (the name is only informational).</summary>
    public bool SameValues(TreeTrunkAdjustment other) =>
        Math.Abs(Scale - other.Scale) < 1e-9 &&
        Math.Abs(OffsetX - other.OffsetX) < 1e-9 &&
        Math.Abs(OffsetY - other.OffsetY) < 1e-9;

    /// <summary>A copy with values rounded to <paramref name="decimals"/> places, as stored in library files.</summary>
    public TreeTrunkAdjustment Rounded(int decimals = 3) =>
        new()
        {
            Name = Name,
            Scale = Math.Round(Scale, decimals),
            OffsetX = Math.Round(OffsetX, decimals),
            OffsetY = Math.Round(OffsetY, decimals),
        };

    public static TreeTrunkAdjustment Identity(string? name = null) => new() { Name = name };
}
