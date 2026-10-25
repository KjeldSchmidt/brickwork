using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Brickwork.Core.Models;

namespace Brickwork.Core.Editing;

/// <summary>
/// A saved Brickwork project: the edited map plus a copy of the source file it was imported from,
/// so the project opens on its own and can be updated from a newer backup of that source.
/// Stored as a zip with <c>project.json</c>, <c>preview.png</c> and <c>source/&lt;file name&gt;</c>.
/// </summary>
public sealed class BrickworkProject
{
    public const string Extension = ".brickwork";

    private const int FormatVersion = 1;
    private const string ProjectEntry = "project.json";
    private const string PreviewEntry = "preview.png";
    private const string SourceFolder = "source/";

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public required MapDocument Map { get; init; }

    /// <summary>Name of the source file (e.g. <c>Zwinger.ink</c>), as imported.</summary>
    public required string SourceFileName { get; init; }

    public required byte[] SourceBytes { get; init; }

    public static void Save(BrickworkProject project, Stream destination)
    {
        var map = (JsonObject)JsonSerializer.SerializeToNode(project.Map, JsonOptions)!;
        // Rebuilt when the map is opened: the image goes in its own entry, the rest is re-derived.
        map.Remove(nameof(MapDocument.PreviewImagePng));
        map.Remove(nameof(MapDocument.Compatibility));
        map.Remove(nameof(MapDocument.UnderlyingMap));

        var root = new JsonObject
        {
            ["format"] = "brickwork-project",
            ["version"] = FormatVersion,
            ["sourceFileName"] = project.SourceFileName,
            ["map"] = map,
        };

        using var zip = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
        using (var writer = new StreamWriter(zip.CreateEntry(ProjectEntry).Open()))
        {
            writer.Write(root.ToJsonString(JsonOptions));
        }

        if (project.Map.PreviewImagePng is { Length: > 0 } preview)
        {
            Write(zip, PreviewEntry, preview, CompressionLevel.NoCompression);
        }

        Write(zip, SourceFolder + project.SourceFileName, project.SourceBytes, CompressionLevel.Optimal);
    }

    public static BrickworkProject Load(Stream source)
    {
        using var zip = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
        var projectEntry = zip.GetEntry(ProjectEntry)
            ?? throw new InvalidDataException("Not a Brickwork project (project.json is missing).");

        JsonObject root;
        using (var stream = projectEntry.Open())
        {
            root = JsonNode.Parse(stream)?.AsObject()
                ?? throw new InvalidDataException("The project file is empty.");
        }

        var version = root["version"]?.GetValue<int>() ?? 0;
        if (version > FormatVersion)
        {
            throw new InvalidDataException("This project was saved by a newer Brickwork. Update Brickwork to open it.");
        }

        var map = root["map"].Deserialize<MapDocument>(JsonOptions)
            ?? throw new InvalidDataException("The project has no map.");
        map.PreviewImagePng = ReadAll(zip.GetEntry(PreviewEntry));

        var sourceFileName = root["sourceFileName"]?.GetValue<string>()
            ?? throw new InvalidDataException("The project does not name its source file.");
        var sourceBytes = ReadAll(zip.GetEntry(SourceFolder + sourceFileName))
            ?? throw new InvalidDataException($"The project is missing its copy of {sourceFileName}.");

        return new BrickworkProject { Map = map, SourceFileName = sourceFileName, SourceBytes = sourceBytes };
    }

    private static void Write(ZipArchive zip, string name, byte[] bytes, CompressionLevel level)
    {
        using var stream = zip.CreateEntry(name, level).Open();
        stream.Write(bytes);
    }

    private static byte[]? ReadAll(ZipArchiveEntry? entry)
    {
        if (entry is null)
        {
            return null;
        }

        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
