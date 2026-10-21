namespace Brickwork.Core.Models;

/// <summary>
/// Per-version trunk adjustments: the built-in values shipped with Brickwork, overlaid with the
/// user's own. Only versions where the user differs from the built-in value (or from "unadjusted"
/// when there is none) are kept as user values, so improved built-in values reach users who never
/// changed that version.
/// </summary>
public sealed class TreeTrunkLibrary
{
    private readonly Dictionary<string, TreeTrunkAdjustment> _builtIn;
    private readonly Dictionary<string, TreeTrunkAdjustment> _user = [];

    public TreeTrunkLibrary(
        IReadOnlyDictionary<string, TreeTrunkAdjustment>? builtIn = null,
        IReadOnlyDictionary<string, TreeTrunkAdjustment>? user = null)
    {
        _builtIn = builtIn?.ToDictionary(entry => entry.Key, entry => entry.Value.Rounded()) ?? [];
        foreach (var (key, value) in user ?? new Dictionary<string, TreeTrunkAdjustment>())
        {
            Set(key, value);
        }
    }

    /// <summary>The user's own values (what gets saved to the user's tree-trunks.json).</summary>
    public IReadOnlyDictionary<string, TreeTrunkAdjustment> UserValues => _user;

    /// <summary>Built-in values overlaid with the user's (what imports use, and what "copy library" exports).</summary>
    public IReadOnlyDictionary<string, TreeTrunkAdjustment> Effective
    {
        get
        {
            var result = new Dictionary<string, TreeTrunkAdjustment>(_builtIn);
            foreach (var (key, value) in _user)
            {
                result[key] = value;
            }

            return result;
        }
    }

    public TreeTrunkAdjustment Get(string key, string? name = null) =>
        _user.TryGetValue(key, out var user) ? user
        : _builtIn.TryGetValue(key, out var builtIn) ? builtIn
        : TreeTrunkAdjustment.Identity(name);

    public void Set(string key, TreeTrunkAdjustment adjustment)
    {
        var rounded = adjustment.Rounded();
        var baseline = _builtIn.TryGetValue(key, out var builtIn) ? builtIn : TreeTrunkAdjustment.Identity();
        if (rounded.SameValues(baseline))
        {
            _user.Remove(key);
        }
        else
        {
            _user[key] = rounded;
        }
    }

    /// <summary>Drops the user's value, returning the version to its built-in (or unadjusted) value.</summary>
    public TreeTrunkAdjustment Reset(string key, string? name = null)
    {
        _user.Remove(key);
        return Get(key, name);
    }
}
