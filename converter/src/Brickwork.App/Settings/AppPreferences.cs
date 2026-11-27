using Brickwork.Core.Export;
using Brickwork.Core.Models;
using Brickwork.Core.Settings;

namespace Brickwork.App.Settings;

/// <summary>Loads session preferences from disk and persists changes.</summary>
public sealed class AppPreferences
{
    private readonly AppSettingsStore _store;
    private readonly TreeTrunkLibraryStore _treeTrunkStore;
    private bool _treeTrunkLibraryDirty;
    private readonly EditorSession _session;
    private bool _suppressSave;

    public AppPreferences(EditorSession session, AppSettingsStore? store = null)
    {
        _session = session;
        _store = store ?? new AppSettingsStore();
        _treeTrunkStore = new TreeTrunkLibraryStore(Path.GetDirectoryName(_store.SettingsFilePath));
        Apply(_store.Load());
        _session.LoadTreeLibrary(new TreeTrunkLibrary(TreeTrunkLibraryStore.LoadBuiltIn(), _treeTrunkStore.Load()));
        // The tree-trunk library is written once, when the app exits (see FlushTreeTrunkLibrary).
        _session.TreeAdjustmentsChanged += (_, _) => _treeTrunkLibraryDirty = true;

        _session.PropertyChanged += (_, args) =>
        {
            if (_suppressSave)
            {
                return;
            }

            if (args.PropertyName is nameof(EditorSession.WallSimplificationTolerance)
                or nameof(EditorSession.ShowDebugPanel)
                or nameof(EditorSession.FoundryMapsPath)
                or nameof(EditorSession.FoundryGridSize)
                or nameof(EditorSession.DetectRegionsFromColorPaths)
                or nameof(EditorSession.DetectRegionsFromNamedContainers)
                or nameof(EditorSession.DetectRegionsFromPathNames)
                or nameof(EditorSession.DetectLightsFromStamps)
                or nameof(EditorSession.LightKeywords)
                or nameof(EditorSession.LightExcludeKeywords)
                or nameof(EditorSession.DetectLightAnimations)
                or nameof(EditorSession.LightBrightRadius)
                or nameof(EditorSession.LightDimRadius)
                or nameof(EditorSession.LightColor)
                or nameof(EditorSession.DetectTreesFromStamps)
                or nameof(EditorSession.TreeKeywords)
                or nameof(EditorSession.TreeExcludeKeywords)
                or nameof(EditorSession.TreeTrunkSize)
                or nameof(EditorSession.TreeTrunkGrowthPercent))
            {
                Save();
            }
        };
    }

    public AppSettingsStore Store => _store;

    /// <summary>Writes tree-trunk library changes, if any; called when the app exits.</summary>
    public void FlushTreeTrunkLibrary()
    {
        if (!_treeTrunkLibraryDirty)
        {
            return;
        }

        _treeTrunkLibraryDirty = false;
        _treeTrunkStore.Save(_session.TreeLibrary.UserValues);
    }

    public void Apply(AppSettings settings)
    {
        _suppressSave = true;
        try
        {
            _session.WallSimplificationTolerance = settings.WallSimplificationTolerance;
            _session.ShowDebugPanel = settings.ShowDebugPanel;
            _session.FoundryMapsPath = settings.FoundryMapsPath ?? string.Empty;
            _session.FoundryGridSize = settings.FoundryGridSize > 0
                ? settings.FoundryGridSize
                : FoundryExportCanvas.DefaultGridSize;
            _session.DetectRegionsFromColorPaths = settings.DetectRegionsFromColorPaths;
            _session.DetectRegionsFromNamedContainers = settings.DetectRegionsFromNamedContainers;
            _session.DetectRegionsFromPathNames = settings.DetectRegionsFromPathNames;
            _session.DetectLightsFromStamps = settings.DetectLightsFromStamps;
            _session.LightKeywords = settings.LightKeywords ?? string.Empty;
            _session.LightExcludeKeywords = settings.LightExcludeKeywords ?? string.Empty;
            _session.DetectLightAnimations = settings.DetectLightAnimations;
            _session.LightBrightRadius = settings.LightBrightRadius;
            _session.LightDimRadius = settings.LightDimRadius;
            _session.LightColor = LightColors.Normalize(settings.LightColor);
            _session.DetectTreesFromStamps = settings.DetectTreesFromStamps;
            _session.TreeKeywords = settings.TreeKeywords ?? string.Empty;
            _session.TreeExcludeKeywords = settings.TreeExcludeKeywords ?? string.Empty;
            _session.TreeTrunkSize = settings.TreeTrunkSize;
            _session.TreeTrunkGrowthPercent = settings.TreeTrunkGrowthPercent;
        }
        finally
        {
            _suppressSave = false;
        }
    }

    public void Save()
    {
        // Preserve update-dismiss state written directly via AppSettingsStore.
        var existing = _store.Load();
        _store.Save(new AppSettings
        {
            WallSimplificationTolerance = _session.WallSimplificationTolerance,
            ShowDebugPanel = _session.ShowDebugPanel,
            FoundryMapsPath = _session.FoundryMapsPath,
            FoundryGridSize = _session.FoundryGridSize,
            DetectRegionsFromColorPaths = _session.DetectRegionsFromColorPaths,
            DetectRegionsFromNamedContainers = _session.DetectRegionsFromNamedContainers,
            DetectRegionsFromPathNames = _session.DetectRegionsFromPathNames,
            DetectLightsFromStamps = _session.DetectLightsFromStamps,
            LightKeywords = _session.LightKeywords,
            LightExcludeKeywords = _session.LightExcludeKeywords,
            DetectLightAnimations = _session.DetectLightAnimations,
            LightBrightRadius = _session.LightBrightRadius,
            LightDimRadius = _session.LightDimRadius,
            LightColor = _session.LightColor,
            DetectTreesFromStamps = _session.DetectTreesFromStamps,
            TreeKeywords = _session.TreeKeywords,
            TreeExcludeKeywords = _session.TreeExcludeKeywords,
            TreeTrunkSize = _session.TreeTrunkSize,
            TreeTrunkGrowthPercent = _session.TreeTrunkGrowthPercent,
            SkippedUpdateVersion = existing.SkippedUpdateVersion,
        });
    }
}
