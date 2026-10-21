using System.Globalization;
using System.Text.Json;

namespace Brickwork.Core.Settings;

/// <summary>
/// Reads and writes path-style names keyed by Inkarnate <c>pathId</c>: the built-in names shipped
/// with Brickwork (<c>resources/path-templates.json</c>, embedded into this assembly) and the user's
/// own (<c>path-templates.json</c> next to <c>settings.json</c>). Both use the same format.
/// </summary>
public sealed class PathTemplateLibraryStore
{
    public const string BuiltInResourceName = "Brickwork.Core.PathTemplateDefaults.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public PathTemplateLibraryStore(string? directory = null)
    {
        var root = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Brickwork");
        FilePath = Path.Combine(root, "path-templates.json");
    }

    public string FilePath { get; }

    /// <summary>The user's names; empty when missing or unreadable.</summary>
    public Dictionary<int, string> Load()
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

    /// <summary>The names shipped with Brickwork; empty when the resource is missing or unreadable.</summary>
    public static Dictionary<int, string> LoadBuiltIn()
    {
        try
        {
            using var stream = typeof(PathTemplateLibraryStore).Assembly.GetManifestResourceStream(BuiltInResourceName);
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

    public void Save(IReadOnlyDictionary<int, string> names)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = FilePath + ".tmp";
        File.WriteAllText(tempPath, Serialize(names));
        File.Move(tempPath, FilePath, overwrite: true);
    }

    /// <summary>JSON in library format, sorted by id.</summary>
    public static string Serialize(IReadOnlyDictionary<int, string> names)
    {
        var file = new LibraryFile
        {
            Names = names
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Value))
                .OrderBy(entry => entry.Key)
                .ToDictionary(entry => entry.Key.ToString(CultureInfo.InvariantCulture), entry => entry.Value.Trim()),
        };
        return JsonSerializer.Serialize(file, SerializerOptions) + Environment.NewLine;
    }

    public static Dictionary<int, string> Deserialize(string json)
    {
        var file = JsonSerializer.Deserialize<LibraryFile>(json, SerializerOptions);
        var result = new Dictionary<int, string>();
        foreach (var (key, name) in file?.Names ?? [])
        {
            if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) &&
                !string.IsNullOrWhiteSpace(name))
            {
                result[id] = name.Trim();
            }
        }

        return result;
    }

    private sealed class LibraryFile
    {
        public Dictionary<string, string> Names { get; set; } = [];
    }
}
