using Brickwork.Composition;
using Brickwork.Core.Editing;

namespace Brickwork.App;

/// <summary>Re-reads the open map's source file with the current import settings and merges the result.</summary>
public static class ImportSettingsApplier
{
    public static bool CanApply(EditorSession session) =>
        session.Map is not null && !string.IsNullOrWhiteSpace(session.SourceFilePath);

    /// <returns>A status message for the user.</returns>
    public static async Task<string> ApplyAsync(EditorSession session, bool wallsAndRegions, bool lights)
    {
        if (session.Map is not { } map || string.IsNullOrWhiteSpace(session.SourceFilePath))
        {
            return "Open a source map first.";
        }

        if (!wallsAndRegions && !lights)
        {
            return "Choose what to apply.";
        }

        var path = session.SourceFilePath;
        if (!File.Exists(path))
        {
            return $"The source file is no longer at {path}.";
        }

        try
        {
            await using var input = File.OpenRead(path);
            var importer = ServiceFactory.CreateImporterForPath(path, session.CreateImportOptions());
            var fresh = await importer.ImportAsync(input).ConfigureAwait(true);

            var result = new MapReimportResult(0, 0, 0);
            session.Execute(
                "Apply import settings",
                () => result = MapReimportMerger.Merge(map, fresh, wallsAndRegions, lights));
            session.ClearWallSelection();
            session.ClearRegionSelection();
            session.FocusedLightEntityId = null;

            return $"Applied: {result.Updated} updated, {result.Added} added, {result.Removed} removed. Undo with Ctrl+Z.";
        }
        catch (Exception ex)
        {
            return $"Could not re-read the source file: {ex.Message}";
        }
    }
}
