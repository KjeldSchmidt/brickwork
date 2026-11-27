using System.Text.Json;
using System.Text.Json.Nodes;

namespace Brickwork.Exporters.Foundry;

public sealed record FoundryWorldInfo(string Id, string Title, string Directory);

public sealed record FoundryFolderInfo(string Id, string Name, string? ParentFolderId, int Sort);

public sealed record FoundrySceneSummary(
    string Id,
    string Name,
    int WallCount,
    int RegionCount,
    string? FolderId = null,
    string FolderPath = "",
    int Sort = 0);

/// <summary>
/// Lists Foundry worlds under a Data root and reads scene documents from LevelDB or legacy NeDB.
/// </summary>
public sealed class FoundryWorldCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public IReadOnlyList<FoundryWorldInfo> ListWorlds(string dataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);

        var worldsDir = Path.Combine(dataRoot, "worlds");
        if (!Directory.Exists(worldsDir))
        {
            return [];
        }

        var worlds = new List<FoundryWorldInfo>();
        foreach (var directory in Directory.EnumerateDirectories(worldsDir))
        {
            var worldJsonPath = Path.Combine(directory, "world.json");
            if (!File.Exists(worldJsonPath))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(worldJsonPath));
                var root = document.RootElement;
                var id = root.TryGetProperty("id", out var idEl)
                    ? idEl.GetString()
                    : Path.GetFileName(directory);
                var title = root.TryGetProperty("title", out var titleEl)
                    ? titleEl.GetString()
                    : id;

                if (string.IsNullOrWhiteSpace(id))
                {
                    id = Path.GetFileName(directory);
                }

                worlds.Add(new FoundryWorldInfo(
                    id!,
                    string.IsNullOrWhiteSpace(title) ? id! : title!,
                    directory));
            }
            catch
            {
                // Skip unreadable world.json
            }
        }

        return worlds
            .OrderBy(world => world.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<FoundrySceneSummary> ListScenes(string dataRoot, string worldId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(worldId);

        var worldDir = Path.Combine(dataRoot, "worlds", worldId);
        if (!Directory.Exists(worldDir))
        {
            throw new DirectoryNotFoundException($"Foundry world not found: {worldId}");
        }

        var folders = LoadSceneFolders(worldDir);
        var folderPaths = BuildFolderPaths(folders);

        var levelDbDir = Path.Combine(worldDir, "data", "scenes");
        if (Directory.Exists(levelDbDir))
        {
            return OrderScenes(ListScenesFromLevelDb(levelDbDir, folderPaths));
        }

        var nedbPath = Path.Combine(worldDir, "data", "scenes.db");
        if (File.Exists(nedbPath))
        {
            return OrderScenes(ListScenesFromNedb(nedbPath, folderPaths));
        }

        return [];
    }

    /// <summary>
    /// Loads a scene document with walls, regions (including behaviors), and levels fully embedded
    /// for mapping.
    /// </summary>
    public JsonObject LoadAssembledScene(string dataRoot, string worldId, string sceneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(worldId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sceneId);

        var worldDir = Path.Combine(dataRoot, "worlds", worldId);
        var levelDbDir = Path.Combine(worldDir, "data", "scenes");
        if (Directory.Exists(levelDbDir))
        {
            return LoadAssembledSceneFromLevelDb(levelDbDir, sceneId);
        }

        var nedbPath = Path.Combine(worldDir, "data", "scenes.db");
        if (File.Exists(nedbPath))
        {
            return LoadAssembledSceneFromNedb(nedbPath, sceneId);
        }

        throw new FileNotFoundException(
            $"No Foundry scenes database found for world '{worldId}'. Expected LevelDB at data/scenes or NeDB at data/scenes.db.");
    }

    private static IReadOnlyList<FoundrySceneSummary> ListScenesFromLevelDb(
        string levelDbDir,
        IReadOnlyDictionary<string, string> folderPaths)
    {
        using var store = FoundryLevelDbStore.OpenReadOnly(levelDbDir);
        var entries = store.EnumerateEntries().ToList();

        var summaries = new List<FoundrySceneSummary>();
        foreach (var entry in entries)
        {
            if (!IsSceneDocumentKey(entry.Key))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(entry.Value);
                var root = document.RootElement;
                var id = root.TryGetProperty("_id", out var idEl)
                    ? idEl.GetString()
                    : entry.Key["!scenes!".Length..];
                var name = root.TryGetProperty("name", out var nameEl)
                    ? nameEl.GetString()
                    : id;

                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var defaultLevelId = FoundrySceneDimensions.ResolveDefaultLevelId(root);
                var wallCount = CountNestedOnDefaultLevel(
                    entries,
                    $"!scenes.walls!{id}.",
                    defaultLevelId);
                var regionCount = CountNestedOnDefaultLevel(
                    entries,
                    $"!scenes.regions!{id}.",
                    defaultLevelId);

                // Embedded / NeDB-style arrays on the scene document (no nested LevelDB docs).
                if (wallCount == 0)
                {
                    wallCount = CountArrayOnDefaultLevel(root, "walls", defaultLevelId);
                }

                if (regionCount == 0)
                {
                    regionCount = CountArrayOnDefaultLevel(root, "regions", defaultLevelId);
                }

                var folderId = ReadNullableString(root, "folder");
                var folderPath = ResolveFolderPath(folderId, folderPaths);
                var sort = root.TryGetProperty("sort", out var sortEl) && sortEl.TryGetInt32(out var sortValue)
                    ? sortValue
                    : 0;

                summaries.Add(new FoundrySceneSummary(
                    id,
                    string.IsNullOrWhiteSpace(name) ? id : name!,
                    wallCount,
                    regionCount,
                    folderId,
                    folderPath,
                    sort));
            }
            catch
            {
                // Skip malformed scene docs
            }
        }

        return summaries;
    }

    private static IReadOnlyList<FoundrySceneSummary> ListScenesFromNedb(
        string nedbPath,
        IReadOnlyDictionary<string, string> folderPaths)
    {
        var summaries = new List<FoundrySceneSummary>();
        foreach (var line in File.ReadLines(nedbPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (root.TryGetProperty("_deleted", out var deleted) &&
                    deleted.ValueKind == JsonValueKind.True)
                {
                    continue;
                }

                var id = root.TryGetProperty("_id", out var idEl) ? idEl.GetString() : null;
                var name = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : id;
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var defaultLevelId = FoundrySceneDimensions.ResolveDefaultLevelId(root);
                var folderId = ReadNullableString(root, "folder");
                var folderPath = ResolveFolderPath(folderId, folderPaths);
                var sort = root.TryGetProperty("sort", out var sortEl) && sortEl.TryGetInt32(out var sortValue)
                    ? sortValue
                    : 0;

                summaries.Add(new FoundrySceneSummary(
                    id,
                    string.IsNullOrWhiteSpace(name) ? id : name!,
                    CountArrayOnDefaultLevel(root, "walls", defaultLevelId),
                    CountArrayOnDefaultLevel(root, "regions", defaultLevelId),
                    folderId,
                    folderPath,
                    sort));
            }
            catch
            {
                // Skip malformed lines
            }
        }

        return summaries;
    }

    private static IReadOnlyList<FoundryFolderInfo> LoadSceneFolders(string worldDir)
    {
        var levelDbDir = Path.Combine(worldDir, "data", "folders");
        if (Directory.Exists(levelDbDir))
        {
            try
            {
                return LoadSceneFoldersFromLevelDb(levelDbDir);
            }
            catch (FoundryWorldDataLockedException)
            {
                throw;
            }
            catch
            {
                // Fall through to NeDB / empty.
            }
        }

        var nedbPath = Path.Combine(worldDir, "data", "folders.db");
        if (File.Exists(nedbPath))
        {
            return LoadSceneFoldersFromNedb(nedbPath);
        }

        return [];
    }

    private static IReadOnlyList<FoundryFolderInfo> LoadSceneFoldersFromLevelDb(string levelDbDir)
    {
        using var store = FoundryLevelDbStore.OpenReadOnly(levelDbDir);
        var folders = new List<FoundryFolderInfo>();
        foreach (var entry in store.EnumerateEntries())
        {
            if (!entry.Key.StartsWith("!folders!", StringComparison.Ordinal) ||
                entry.Key.StartsWith("!folders.", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(entry.Value);
                var folder = TryReadSceneFolder(document.RootElement, entry.Key["!folders!".Length..]);
                if (folder is not null)
                {
                    folders.Add(folder);
                }
            }
            catch
            {
                // Skip malformed
            }
        }

        return folders;
    }

    private static IReadOnlyList<FoundryFolderInfo> LoadSceneFoldersFromNedb(string nedbPath)
    {
        var folders = new List<FoundryFolderInfo>();
        foreach (var line in File.ReadLines(nedbPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (root.TryGetProperty("_deleted", out var deleted) &&
                    deleted.ValueKind == JsonValueKind.True)
                {
                    continue;
                }

                var folder = TryReadSceneFolder(root, fallbackId: null);
                if (folder is not null)
                {
                    folders.Add(folder);
                }
            }
            catch
            {
                // Skip malformed
            }
        }

        return folders;
    }

    private static FoundryFolderInfo? TryReadSceneFolder(JsonElement root, string? fallbackId)
    {
        var type = root.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
        if (!string.Equals(type, "Scene", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var id = root.TryGetProperty("_id", out var idEl) ? idEl.GetString() : fallbackId;
        var name = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : id;
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var sort = root.TryGetProperty("sort", out var sortEl) && sortEl.TryGetInt32(out var sortValue)
            ? sortValue
            : 0;

        return new FoundryFolderInfo(
            id,
            string.IsNullOrWhiteSpace(name) ? id : name!,
            ReadNullableString(root, "folder"),
            sort);
    }

    private static IReadOnlyDictionary<string, string> BuildFolderPaths(
        IReadOnlyList<FoundryFolderInfo> folders)
    {
        var byId = folders.ToDictionary(folder => folder.Id, StringComparer.Ordinal);
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var folder in folders)
        {
            paths[folder.Id] = BuildSingleFolderPath(folder.Id, byId);
        }

        return paths;
    }

    private static string BuildSingleFolderPath(
        string folderId,
        IReadOnlyDictionary<string, FoundryFolderInfo> byId)
    {
        var segments = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var currentId = folderId;

        while (!string.IsNullOrWhiteSpace(currentId) && seen.Add(currentId))
        {
            if (!byId.TryGetValue(currentId, out var folder))
            {
                break;
            }

            segments.Add(folder.Name);
            currentId = folder.ParentFolderId ?? string.Empty;
        }

        segments.Reverse();
        return string.Join(" / ", segments);
    }

    private static string ResolveFolderPath(
        string? folderId,
        IReadOnlyDictionary<string, string> folderPaths)
    {
        if (string.IsNullOrWhiteSpace(folderId))
        {
            return string.Empty;
        }

        return folderPaths.TryGetValue(folderId, out var path) ? path : string.Empty;
    }

    private static IReadOnlyList<FoundrySceneSummary> OrderScenes(
        IReadOnlyList<FoundrySceneSummary> scenes) =>
        scenes
            .OrderBy(scene => scene.FolderPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(scene => scene.Sort)
            .ThenBy(scene => scene.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string? ReadNullableString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static JsonObject LoadAssembledSceneFromLevelDb(string levelDbDir, string sceneId)
    {
        using var store = FoundryLevelDbStore.OpenReadOnly(levelDbDir);
        var sceneJson = store.TryGet($"!scenes!{sceneId}")
            ?? throw new KeyNotFoundException($"Scene '{sceneId}' was not found in the Foundry world database.");

        var scene = JsonNode.Parse(sceneJson)?.AsObject()
            ?? throw new InvalidOperationException($"Scene '{sceneId}' JSON could not be parsed.");

        var wallsById = LoadNestedDocuments(store, $"!scenes.walls!{sceneId}.");
        var regionsById = LoadNestedDocuments(store, $"!scenes.regions!{sceneId}.");
        var levelsById = LoadNestedDocuments(store, $"!scenes.levels!{sceneId}.");
        var behaviors = LoadBehaviorDocuments(store, sceneId);

        scene["walls"] = ResolveEmbeddedArray(scene["walls"], wallsById);
        scene["levels"] = ResolveEmbeddedArray(scene["levels"], levelsById);

        var regionArray = new JsonArray();
        foreach (var regionNode in ResolveEmbeddedArrayItems(scene["regions"], regionsById))
        {
            if (regionNode is not JsonObject regionObj)
            {
                continue;
            }

            var regionId = regionObj["_id"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(regionId) &&
                behaviors.TryGetValue(regionId, out var regionBehaviors))
            {
                regionObj["behaviors"] = new JsonArray(regionBehaviors.ToArray());
            }
            else if (regionObj["behaviors"] is JsonArray existing &&
                     existing.All(item => item is JsonValue))
            {
                // ID-only behavior refs with no nested docs — clear to empty.
                regionObj["behaviors"] = new JsonArray();
            }

            regionArray.Add(regionObj);
        }

        scene["regions"] = regionArray;
        return scene;
    }

    private static JsonObject LoadAssembledSceneFromNedb(string nedbPath, string sceneId)
    {
        foreach (var line in File.ReadLines(nedbPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JsonObject? scene;
            try
            {
                scene = JsonNode.Parse(line)?.AsObject();
            }
            catch
            {
                continue;
            }

            if (scene is null)
            {
                continue;
            }

            var id = scene["_id"]?.GetValue<string>();
            if (!string.Equals(id, sceneId, StringComparison.Ordinal))
            {
                continue;
            }

            if (scene["_deleted"] is JsonValue deleted &&
                deleted.GetValue<bool>())
            {
                throw new KeyNotFoundException($"Scene '{sceneId}' is marked deleted in NeDB.");
            }

            return scene;
        }

        throw new KeyNotFoundException($"Scene '{sceneId}' was not found in scenes.db.");
    }

    private static Dictionary<string, JsonObject> LoadNestedDocuments(
        FoundryLevelDbStore store,
        string prefix)
    {
        var result = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var entry in store.EnumerateByKeyPrefix(prefix))
        {
            var id = entry.Key[prefix.Length..];
            // Skip deeper nested keys (e.g. accidental prefix collisions).
            if (id.Contains('.', StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                var node = JsonNode.Parse(entry.Value)?.AsObject();
                if (node is not null)
                {
                    result[id] = node;
                }
            }
            catch
            {
                // Skip malformed
            }
        }

        return result;
    }

    private static Dictionary<string, List<JsonObject>> LoadBehaviorDocuments(
        FoundryLevelDbStore store,
        string sceneId)
    {
        var prefix = $"!scenes.regions.behaviors!{sceneId}.";
        var result = new Dictionary<string, List<JsonObject>>(StringComparer.Ordinal);

        foreach (var entry in store.EnumerateByKeyPrefix(prefix))
        {
            var remainder = entry.Key[prefix.Length..];
            var dot = remainder.IndexOf('.', StringComparison.Ordinal);
            if (dot <= 0)
            {
                continue;
            }

            var regionId = remainder[..dot];
            try
            {
                var node = JsonNode.Parse(entry.Value)?.AsObject();
                if (node is null)
                {
                    continue;
                }

                if (!result.TryGetValue(regionId, out var list))
                {
                    list = [];
                    result[regionId] = list;
                }

                list.Add(node);
            }
            catch
            {
                // Skip malformed
            }
        }

        return result;
    }

    private static JsonArray ResolveEmbeddedArray(JsonNode? refsNode, Dictionary<string, JsonObject> byId)
    {
        var array = new JsonArray();
        foreach (var item in ResolveEmbeddedArrayItems(refsNode, byId))
        {
            array.Add(item);
        }

        return array;
    }

    private static IEnumerable<JsonNode> ResolveEmbeddedArrayItems(
        JsonNode? refsNode,
        Dictionary<string, JsonObject> byId)
    {
        if (refsNode is not JsonArray refs)
        {
            foreach (var embedded in byId.Values)
            {
                yield return embedded.DeepClone();
            }

            yield break;
        }

        if (refs.Count > 0 && refs[0] is JsonObject)
        {
            foreach (var item in refs)
            {
                if (item is not null)
                {
                    yield return item.DeepClone();
                }
            }

            yield break;
        }

        foreach (var item in refs)
        {
            var id = item?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (byId.TryGetValue(id, out var doc))
            {
                yield return doc.DeepClone();
            }
        }
    }

    private static bool IsSceneDocumentKey(string key) =>
        key.StartsWith("!scenes!", StringComparison.Ordinal) &&
        !key.StartsWith("!scenes.", StringComparison.Ordinal);

    private static int CountNestedOnDefaultLevel(
        IReadOnlyList<KeyValuePair<string, string>> entries,
        string prefix,
        string? defaultLevelId)
    {
        var count = 0;
        foreach (var entry in entries)
        {
            if (!entry.Key.StartsWith(prefix, StringComparison.Ordinal) ||
                entry.Key[prefix.Length..].Contains('.', StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(entry.Value);
                if (FoundrySceneDimensions.BelongsToDefaultLevel(document.RootElement, defaultLevelId))
                {
                    count++;
                }
            }
            catch
            {
                // Skip malformed nested docs
            }
        }

        return count;
    }

    private static int CountArrayOnDefaultLevel(
        JsonElement parent,
        string propertyName,
        string? defaultLevelId)
    {
        if (!parent.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var count = 0;
        foreach (var item in value.EnumerateArray())
        {
            // ID-only refs (LevelDB) can't be filtered here; caller uses nested docs instead.
            if (item.ValueKind != JsonValueKind.Object)
            {
                count++;
                continue;
            }

            if (FoundrySceneDimensions.BelongsToDefaultLevel(item, defaultLevelId))
            {
                count++;
            }
        }

        return count;
    }
}
