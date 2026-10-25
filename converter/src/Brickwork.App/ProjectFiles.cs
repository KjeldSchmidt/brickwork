using Brickwork.Composition;
using Brickwork.Core.Editing;
using Brickwork.Core.Models;

namespace Brickwork.App;

/// <summary>Opens, saves and updates <c>.brickwork</c> projects for the editor session.</summary>
public static class ProjectFiles
{
    public static bool IsProjectPath(string path) =>
        string.Equals(Path.GetExtension(path), BrickworkProject.Extension, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Loads a project. Its copy of the source file is written to a temporary folder, which becomes
    /// the session's source file (for re-applying import settings and for updating the project).
    /// </summary>
    public static async Task OpenAsync(EditorSession session, string path)
    {
        BrickworkProject project;
        await using (var input = File.OpenRead(path))
        {
            project = BrickworkProject.Load(input);
        }

        var sourceFolder = Path.Combine(Path.GetTempPath(), "Brickwork", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sourceFolder);
        var sourcePath = Path.Combine(sourceFolder, project.SourceFileName);
        await File.WriteAllBytesAsync(sourcePath, project.SourceBytes).ConfigureAwait(true);

        var map = project.Map;
        map.SourceFileName = project.SourceFileName;
        session.SceneLevelCount = Math.Clamp(map.SceneLevelCount, 1, SceneLevels.MaxCount);
        session.SceneLevelHeight = map.SceneLevelHeight > 0 ? map.SceneLevelHeight : SceneLevels.DefaultHeight;
        session.GlobalIllumination = map.GlobalIllumination;
        session.SourceFilePath = sourcePath;
        session.ProjectFilePath = path;
        session.Map = map;
        session.SourceFileName = project.SourceFileName;
    }

    /// <summary>Saves the open map with a copy of its source file. Writes a temporary file first, so a failed save keeps the old project.</summary>
    public static async Task SaveAsync(EditorSession session, string path)
    {
        if (session.Map is not { } map || string.IsNullOrWhiteSpace(session.SourceFilePath))
        {
            throw new InvalidOperationException("Open a source map first.");
        }

        if (!File.Exists(session.SourceFilePath))
        {
            throw new FileNotFoundException($"The source file is no longer at {session.SourceFilePath}.");
        }

        map.SceneLevelCount = session.SceneLevelCount;
        map.SceneLevelHeight = session.SceneLevelHeight;
        map.GlobalIllumination = session.GlobalIllumination;
        var sourceFileName = session.SourceFileName ?? Path.GetFileName(session.SourceFilePath);
        var project = new BrickworkProject
        {
            Map = map,
            SourceFileName = sourceFileName,
            SourceBytes = await File.ReadAllBytesAsync(session.SourceFilePath).ConfigureAwait(true),
        };

        var temporaryPath = path + ".saving";
        await using (var output = File.Create(temporaryPath))
        {
            BrickworkProject.Save(project, output);
        }

        File.Move(temporaryPath, path, overwrite: true);
        session.ProjectFilePath = path;
    }

    /// <summary>
    /// Brings a newer backup of the source map into the open project: the changes made in
    /// Brickwork are applied to the backup (see <see cref="ProjectUpdateMerger"/>), which becomes
    /// the project's source. The update is not saved until the project is.
    /// </summary>
    /// <returns>A status message for the user.</returns>
    public static async Task<string> UpdateAsync(EditorSession session, string backupPath)
    {
        if (session.Map is not { } map || string.IsNullOrWhiteSpace(session.SourceFilePath))
        {
            return "Open a project first.";
        }

        var previousImport = await ImportAsync(session, session.SourceFilePath).ConfigureAwait(true);
        var newImport = await ImportAsync(session, backupPath).ConfigureAwait(true);
        map.SceneLevelCount = session.SceneLevelCount;
        map.SceneLevelHeight = session.SceneLevelHeight;
        map.GlobalIllumination = session.GlobalIllumination;
        var result = ProjectUpdateMerger.Merge(map, previousImport, newImport);

        var sourceFileName = Path.GetFileName(backupPath);
        result.Map.SourceFileName = sourceFileName;
        session.SourceFilePath = backupPath;
        session.Map = result.Map;
        session.SourceFileName = sourceFileName;
        return $"Updated from {sourceFileName}: {result.Updated} kept, {result.Added} new, {result.Removed} removed. Save the project to keep it.";
    }

    private static async Task<MapDocument> ImportAsync(EditorSession session, string path)
    {
        await using var input = File.OpenRead(path);
        var importer = ServiceFactory.CreateImporterForPath(path, session.CreateImportOptions());
        return await importer.ImportAsync(input).ConfigureAwait(true);
    }
}
