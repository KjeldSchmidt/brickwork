using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Brickwork.Core.Foundry;
using Brickwork.Core.Models;
using Brickwork.Core.Settings;

namespace Brickwork.App.ViewModels;

public partial class SettingsWindowViewModel : ObservableObject
{
    private readonly EditorSession _session;

    [ObservableProperty]
    private SettingsCategory _selectedCategory = SettingsCategory.Import;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyToOpenMapCommand))]
    private bool _applyWallsAndRegions = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyToOpenMapCommand))]
    private bool _applyLights = true;

    [ObservableProperty]
    private string? _applyStatus;

    public SettingsWindowViewModel(EditorSession session)
    {
        _session = session;
        _session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(EditorSession.WallSimplificationTolerance))
            {
                OnPropertyChanged(nameof(WallSimplificationTolerance));
            }

            if (args.PropertyName is nameof(EditorSession.ShowDebugPanel))
            {
                OnPropertyChanged(nameof(ShowDebugPanel));
            }

            if (args.PropertyName is nameof(EditorSession.FoundryMapsPath))
            {
                OnPropertyChanged(nameof(FoundryMapsPath));
            }

            if (args.PropertyName is nameof(EditorSession.FoundryGridSize))
            {
                OnPropertyChanged(nameof(FoundryGridSize));
            }

            if (args.PropertyName is nameof(EditorSession.DetectRegionsFromColorPaths))
            {
                OnPropertyChanged(nameof(DetectRegionsFromColorPaths));
            }

            if (args.PropertyName is nameof(EditorSession.DetectRegionsFromNamedContainers))
            {
                OnPropertyChanged(nameof(DetectRegionsFromNamedContainers));
            }

            if (args.PropertyName is nameof(EditorSession.DetectRegionsFromPathNames))
            {
                OnPropertyChanged(nameof(DetectRegionsFromPathNames));
            }

            if (args.PropertyName is nameof(EditorSession.Map) or nameof(EditorSession.SourceFilePath))
            {
                ApplyToOpenMapCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(OpenMapDescription));
            }

            if (args.PropertyName is nameof(EditorSession.DetectLightsFromStamps)
                or nameof(EditorSession.DetectLightAnimations)
                or nameof(EditorSession.LightKeywords)
                or nameof(EditorSession.LightExcludeKeywords)
                or nameof(EditorSession.LightBrightRadius)
                or nameof(EditorSession.LightDimRadius)
                or nameof(EditorSession.LightColor)
                or nameof(EditorSession.DetectTreesFromStamps)
                or nameof(EditorSession.TreeKeywords)
                or nameof(EditorSession.TreeExcludeKeywords)
                or nameof(EditorSession.TreeTrunkSize)
                or nameof(EditorSession.TreeTrunkGrowthPercent))
            {
                OnPropertyChanged(args.PropertyName);
            }
        };
    }

    public bool IsImportSelected => SelectedCategory == SettingsCategory.Import;

    public bool IsExportSelected => SelectedCategory == SettingsCategory.Export;

    public bool IsGeneralSelected => SelectedCategory == SettingsCategory.General;

    public double WallSimplificationTolerance
    {
        get => _session.WallSimplificationTolerance;
        set => _session.WallSimplificationTolerance = value;
    }

    public bool DetectRegionsFromColorPaths
    {
        get => _session.DetectRegionsFromColorPaths;
        set => _session.DetectRegionsFromColorPaths = value;
    }

    public bool DetectRegionsFromNamedContainers
    {
        get => _session.DetectRegionsFromNamedContainers;
        set => _session.DetectRegionsFromNamedContainers = value;
    }

    public bool DetectRegionsFromPathNames
    {
        get => _session.DetectRegionsFromPathNames;
        set => _session.DetectRegionsFromPathNames = value;
    }

    public bool DetectLightsFromStamps
    {
        get => _session.DetectLightsFromStamps;
        set => _session.DetectLightsFromStamps = value;
    }

    public string LightKeywords
    {
        get => _session.LightKeywords;
        set => _session.LightKeywords = value ?? string.Empty;
    }

    public string LightExcludeKeywords
    {
        get => _session.LightExcludeKeywords;
        set => _session.LightExcludeKeywords = value ?? string.Empty;
    }

    public bool DetectLightAnimations
    {
        get => _session.DetectLightAnimations;
        set => _session.DetectLightAnimations = value;
    }

    public string OpenMapDescription => ImportSettingsApplier.CanApply(_session)
        ? $"Re-reads {Path.GetFileName(_session.SourceFilePath)} with the settings above and rebuilds the chosen parts of the open map."
        : "Open a source map to apply changed settings to it. Otherwise they apply the next time you open a map.";

    private bool CanApplyToOpenMap() =>
        ImportSettingsApplier.CanApply(_session) && (ApplyWallsAndRegions || ApplyLights);

    [RelayCommand(CanExecute = nameof(CanApplyToOpenMap))]
    private async Task ApplyToOpenMapAsync()
    {
        ApplyStatus = "Applying…";
        ApplyStatus = await ImportSettingsApplier.ApplyAsync(_session, ApplyWallsAndRegions, ApplyLights)
            .ConfigureAwait(true);
    }

    public double LightBrightRadius
    {
        get => _session.LightBrightRadius;
        set => _session.LightBrightRadius = value;
    }

    public double LightDimRadius
    {
        get => _session.LightDimRadius;
        set => _session.LightDimRadius = value;
    }

    /// <summary>Default light color as #rrggbb; invalid input is ignored.</summary>
    public string LightColor
    {
        get => _session.LightColor;
        set
        {
            if (LightColors.TryParse(value, out _, out _, out _))
            {
                _session.LightColor = LightColors.Normalize(value);
            }
        }
    }

    public bool DetectTreesFromStamps
    {
        get => _session.DetectTreesFromStamps;
        set => _session.DetectTreesFromStamps = value;
    }

    public string TreeKeywords
    {
        get => _session.TreeKeywords;
        set => _session.TreeKeywords = value ?? string.Empty;
    }

    public string TreeExcludeKeywords
    {
        get => _session.TreeExcludeKeywords;
        set => _session.TreeExcludeKeywords = value ?? string.Empty;
    }

    public double TreeTrunkSize
    {
        get => _session.TreeTrunkSize;
        set => _session.TreeTrunkSize = value;
    }

    public double TreeTrunkGrowthPercent
    {
        get => _session.TreeTrunkGrowthPercent;
        set => _session.TreeTrunkGrowthPercent = value;
    }

    [ObservableProperty]
    private string? _treeLibraryStatus;

    /// <summary>
    /// Copies all tree version adjustments (built-in values plus the user's) as JSON. Pasting it over
    /// resources/tree-trunks.json in the repository updates Brickwork's built-in values.
    /// </summary>
    [RelayCommand]
    private async Task CopyTreeLibraryAsync()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.Clipboard is not { } clipboard)
        {
            TreeLibraryStatus = "Clipboard not available.";
            return;
        }

        var library = _session.TreeLibrary.Effective;
        await clipboard.SetTextAsync(TreeTrunkLibraryStore.Serialize(library)).ConfigureAwait(true);
        TreeLibraryStatus = $"Copied {library.Count(entry => !entry.Value.Rounded().IsIdentity)} tree versions.";
    }

    [RelayCommand]
    private void ResetTreeKeywords()
    {
        TreeKeywords = TreeTrunks.DefaultInclude;
        TreeExcludeKeywords = TreeTrunks.DefaultExclude;
    }

    [RelayCommand]
    private void ResetLightKeywords()
    {
        LightKeywords = Core.Models.LightKeywords.DefaultInclude;
        LightExcludeKeywords = Core.Models.LightKeywords.DefaultExclude;
    }

    public bool ShowDebugPanel
    {
        get => _session.ShowDebugPanel;
        set => _session.ShowDebugPanel = value;
    }

    public string FoundryMapsPath
    {
        get => _session.FoundryMapsPath;
        set => _session.FoundryMapsPath = value ?? string.Empty;
    }

    public int FoundryGridSize
    {
        get => _session.FoundryGridSize;
        set => _session.FoundryGridSize = Math.Max(1, value);
    }

    public void SelectCategory(SettingsCategory category) => SelectedCategory = category;

    [RelayCommand]
    private void SelectImport() => SelectCategory(SettingsCategory.Import);

    [RelayCommand]
    private void SelectExport() => SelectCategory(SettingsCategory.Export);

    [RelayCommand]
    private void SelectGeneral() => SelectCategory(SettingsCategory.General);

    [RelayCommand]
    private async Task BrowseFoundryMapsPathAsync()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.StorageProvider is not { } storageProvider)
        {
            return;
        }

        IStorageFolder? startLocation = null;
        var startPath = !string.IsNullOrWhiteSpace(FoundryMapsPath) && Directory.Exists(FoundryMapsPath)
            ? FoundryMapsPath
            : FoundryPaths.TryGetPreferredMapsDirectory()
              ?? FoundryPaths.TryGetDefaultDataDirectory()
              ?? FoundryPaths.TryGetDefaultUserDataDirectory();

        if (!string.IsNullOrWhiteSpace(startPath) && Directory.Exists(startPath))
        {
            startLocation = await storageProvider.TryGetFolderFromPathAsync(startPath).ConfigureAwait(true);
        }

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Foundry maps folder",
            AllowMultiple = false,
            SuggestedStartLocation = startLocation,
        }).ConfigureAwait(true);

        if (folders.Count == 0)
        {
            return;
        }

        var path = folders[0].TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            FoundryMapsPath = path;
        }
    }

    partial void OnSelectedCategoryChanged(SettingsCategory value)
    {
        OnPropertyChanged(nameof(IsImportSelected));
        OnPropertyChanged(nameof(IsExportSelected));
        OnPropertyChanged(nameof(IsGeneralSelected));
    }
}
