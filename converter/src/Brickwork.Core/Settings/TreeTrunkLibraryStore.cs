using System.Text.Json;
using Brickwork.Core.Models;

namespace Brickwork.Core.Settings;

/// <summary>
/// Reads and writes tree-trunk libraries: per-version trunk adjustments keyed by
/// <see cref="TreeTrunks.VersionKey(int?, string?)"/>. Inkarnate asset ids are the same in every map,
/// so an adjusted tree version is right on all maps that use it.
/// </summary>
/// <remarks>
/// There are two libraries in the same JSON format:
/// <list type="bullet">
/// <item>the built-in one shipped with Brickwork (<c>resources/tree-trunks.json</c> in the repository,
/// embedded into this assembly), and</item>
/// <item>the user's own (<c>tree-trunks.json</c> next to <c>settings.json</c>), holding only the
/// versions where the user differs from the built-in values.</item>
/// </list>
/// <see cref="Serialize"/> produces the same format, so a library copied from the app can be pasted
/// over <c>resources/tree-trunks.json</c> to update the built-in values.
/// </remarks>
public sealed class TreeTrunkLibraryStore
{
    public const string BuiltInResourceName = "Brickwork.Core.TreeTrunkDefaults.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public TreeTrunkLibraryStore(string? directory = null)
    {
        var root = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Brickwork");
        FilePath = Path.Combine(root, "tree-trunks.json");
    }

    public string FilePath { get; }

    /// <summary>The user's library; empty when missing or unreadable.</summary>
    public Dictionary<string, TreeTrunkAdjustment> Load()
    {
        try
        {
            return File.Exists(FilePath) ? Deserialize(File.ReadAllText(FilePath)) : [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>The library shipped with Brickwork; empty when the resource is missing or unreadable.</summary>
    public static Dictionary<string, TreeTrunkAdjustment> LoadBuiltIn()
    {
        try
        {
            using var stream = typeof(TreeTrunkLibraryStore).Assembly.GetManifestResourceStream(BuiltInResourceName);
            if (stream is null)
            {
                return [];
            }

            using var reader = new StreamReader(stream);
            return Deserialize(reader.ReadToEnd());
        }
        catch
        {
            return [];
        }
    }

    public void Save(IReadOnlyDictionary<string, TreeTrunkAdjustment> versions)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = FilePath + ".tmp";
        File.WriteAllText(tempPath, Serialize(versions));
        File.Move(tempPath, FilePath, overwrite: true);
    }

    /// <summary>JSON in library format: sorted by key, values rounded, unadjusted versions left out.</summary>
    public static string Serialize(IReadOnlyDictionary<string, TreeTrunkAdjustment> versions)
    {
        var file = new LibraryFile
        {
            Versions = versions
                .Select(entry => (entry.Key, Value: entry.Value.Rounded()))
                .Where(entry => !entry.Value.IsIdentity)
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .ToDictionary(entry => entry.Key, entry => entry.Value),
        };
        return JsonSerializer.Serialize(file, SerializerOptions) + Environment.NewLine;
    }

    public static Dictionary<string, TreeTrunkAdjustment> Deserialize(string json)
    {
        var file = JsonSerializer.Deserialize<LibraryFile>(json, SerializerOptions);
        return file?.Versions is { } versions
            ? versions
                .Where(entry => entry.Value is { Scale: > 0 })
                .ToDictionary(entry => entry.Key, entry => entry.Value)
            : [];
    }

    private sealed class LibraryFile
    {
        public Dictionary<string, TreeTrunkAdjustment> Versions { get; set; } = [];
    }
}
