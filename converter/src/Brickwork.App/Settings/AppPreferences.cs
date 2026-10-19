using Brickwork.Core.Settings;

namespace Brickwork.App.Settings;

/// <summary>Loads session preferences from disk and persists changes.</summary>
public sealed class AppPreferences
{
    private readonly AppSettingsStore _store;
    private readonly EditorSession _session;
    private bool _suppressSave;

    public AppPreferences(EditorSession session, AppSettingsStore? store = null)
    {
        _session = session;
        _store = store ?? new AppSettingsStore();
        Apply(_store.Load());

        _session.PropertyChanged += (_, args) =>
        {
            if (_suppressSave)
            {
                return;
            }

            if (args.PropertyName is nameof(EditorSession.WallSimplificationTolerance)
                or nameof(EditorSession.ShowDebugPanel)
                or nameof(EditorSession.FoundryMapsPath)
                or nameof(EditorSession.DetectRegionsFromColorPaths)
                or nameof(EditorSession.DetectRegionsFromNamedContainers)
                or nameof(EditorSession.DetectRegionsFromPathNames)
                or nameof(EditorSession.DetectLightsFromStamps)
                or nameof(EditorSession.LightKeywords)
                or nameof(EditorSession.LightExcludeKeywords)
                or nameof(EditorSession.DetectLightAnimations)
                or nameof(EditorSession.LightBrightRadius)
                or nameof(EditorSession.LightDimRadius))
            {
                Save();
            }
        };
    }

    public AppSettingsStore Store => _store;

    public void Apply(AppSettings settings)
    {
        _suppressSave = true;
        try
        {
            _session.WallSimplificationTolerance = settings.WallSimplificationTolerance;
            _session.ShowDebugPanel = settings.ShowDebugPanel;
            _session.FoundryMapsPath = settings.FoundryMapsPath ?? string.Empty;
            _session.DetectRegionsFromColorPaths = settings.DetectRegionsFromColorPaths;
            _session.DetectRegionsFromNamedContainers = settings.DetectRegionsFromNamedContainers;
            _session.DetectRegionsFromPathNames = settings.DetectRegionsFromPathNames;
            _session.DetectLightsFromStamps = settings.DetectLightsFromStamps;
            _session.LightKeywords = settings.LightKeywords ?? string.Empty;
            _session.LightExcludeKeywords = settings.LightExcludeKeywords ?? string.Empty;
            _session.DetectLightAnimations = settings.DetectLightAnimations;
            _session.LightBrightRadius = settings.LightBrightRadius;
            _session.LightDimRadius = settings.LightDimRadius;
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
            DetectRegionsFromColorPaths = _session.DetectRegionsFromColorPaths,
            DetectRegionsFromNamedContainers = _session.DetectRegionsFromNamedContainers,
            DetectRegionsFromPathNames = _session.DetectRegionsFromPathNames,
            DetectLightsFromStamps = _session.DetectLightsFromStamps,
            LightKeywords = _session.LightKeywords,
            LightExcludeKeywords = _session.LightExcludeKeywords,
            DetectLightAnimations = _session.DetectLightAnimations,
            LightBrightRadius = _session.LightBrightRadius,
            LightDimRadius = _session.LightDimRadius,
            SkippedUpdateVersion = existing.SkippedUpdateVersion,
        });
    }
}
