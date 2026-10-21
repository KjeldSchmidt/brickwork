namespace Brickwork.Core.Models;

/// <summary>
/// Names for Inkarnate path styles (<see cref="Wall.PathTemplateId"/>). Inkarnate keeps these names
/// on its servers, not in the map file, so they are named in Brickwork: the built-in names shipped with
/// Brickwork, overlaid with the user's own. Path ids are the same in every map, so a name applies to
/// all maps.
/// </summary>
public sealed class PathTemplateLibrary
{
    /// <summary>Inkarnate's default name for a path.</summary>
    public const string DefaultWallName = "Wall";

    private readonly Dictionary<int, string> _builtIn;
    private readonly Dictionary<int, string> _user = [];

    public PathTemplateLibrary(
        IReadOnlyDictionary<int, string>? builtIn = null,
        IReadOnlyDictionary<int, string>? user = null)
    {
        _builtIn = builtIn?
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Value))
            .ToDictionary(entry => entry.Key, entry => entry.Value.Trim()) ?? [];
        foreach (var (id, name) in user ?? new Dictionary<int, string>())
        {
            SetName(id, name);
        }
    }

    /// <summary>The user's own names (what gets saved to the user's path-templates.json).</summary>
    public IReadOnlyDictionary<int, string> UserValues => _user;

    /// <summary>The name for <paramref name="id"/>, or null when it has none.</summary>
    public string? GetName(int id) =>
        _user.TryGetValue(id, out var user) ? user
        : _builtIn.TryGetValue(id, out var builtIn) ? builtIn
        : null;

    /// <summary>Names a path style; a blank name returns it to its built-in name (or none).</summary>
    public void SetName(int id, string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed) ||
            (_builtIn.TryGetValue(id, out var builtIn) && builtIn == trimmed))
        {
            _user.Remove(id);
        }
        else
        {
            _user[id] = trimmed;
        }
    }

    /// <summary>The style's name, or "Path {id}" when it has none.</summary>
    public string TemplateDisplayName(int id) => GetName(id) ?? $"Path {id}";

    /// <summary>
    /// A wall's name for lists: its own name if it has one, otherwise its path style's name.
    /// A split part like "Wall (hole 1)" keeps its suffix ("Rope Fence (hole 1)").
    /// </summary>
    public string DisplayName(Wall wall)
    {
        var name = wall.Name?.Trim();
        if (wall.PathTemplateId is not int id || !HasDefaultName(name))
        {
            return wall.DisplayName;
        }

        var suffix = string.IsNullOrEmpty(name) ? string.Empty : name[DefaultWallName.Length..];
        return TemplateDisplayName(id) + suffix;
    }

    private static bool HasDefaultName(string? name) =>
        string.IsNullOrEmpty(name) ||
        name == DefaultWallName ||
        name.StartsWith(DefaultWallName + " (", StringComparison.Ordinal);
}
