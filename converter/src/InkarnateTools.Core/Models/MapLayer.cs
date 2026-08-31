namespace InkarnateTools.Core.Models;

public sealed class MapLayer
{
    public string Id { get; init; } = string.Empty;

    public string? Name { get; set; }

    public string? Kind { get; set; }

    public bool IsVisible { get; set; } = true;

    public int Order { get; set; }

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Name) ? Id : Name;
}
