using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Brickwork.App.Foundry;
using Brickwork.App.Views.Dialogs;
using Brickwork.Composition;
using Brickwork.Core.Editing;
using Brickwork.Core.Models;
using Brickwork.Core.Services;
using Brickwork.Inkarnate;
using IconPacks.Avalonia.FontAwesome6;

namespace Brickwork.App.ViewModels;

public partial class ImportToolViewModel : Tool
{
    private static readonly IBrush UnderlyingMapMissingBrush = new SolidColorBrush(Color.Parse("#9CA3AF"));
    private static readonly IBrush UnderlyingMapFoundBrush = new SolidColorBrush(Color.Parse("#22C55E"));

    private readonly EditorSession _session;
    private readonly ConvertMapService _convertMapService = ServiceFactory.CreateGuiConvertMapService();
    private readonly IReadOnlyList<ExportFormatChoice> _exportFormatChoices;

    [ObservableProperty]
    private string _statusMessage = "Ready.";

    [ObservableProperty]
    private string _underlyingMapTooltip = string.Empty;

    [ObservableProperty]
    private bool _showUnderlyingMapStatus;

    [ObservableProperty]
    private PackIconFontAwesome6Kind _underlyingMapLinkIconKind = PackIconFontAwesome6Kind.LinkSlashSolid;

    [ObservableProperty]
    private IBrush _underlyingMapLinkBrush = UnderlyingMapMissingBrush;

    [ObservableProperty]
    private bool _showCompatibilityWarning;

    [ObservableProperty]
    private string _compatibilityWarningMessage = string.Empty;

    private bool _compatibilityWarningDismissed;

    /// <summary>Invoked when the File dock should grow/shrink for the compatibility banner.</summary>
    public Action<bool>? FileDockExpandedChanged { get; set; }

    public ImportToolViewModel(EditorSession session)
    {
        _session = session;
        _exportFormatChoices = ExportFormatChoice.ForFormats(_convertMapService.SupportedExportFormats);
        _session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(EditorSession.Map))
            {
                ConvertCommand.NotifyCanExecuteChanged();
                SaveProjectCommand.NotifyCanExecuteChanged();
                SaveProjectAsCommand.NotifyCanExecuteChanged();
                UpdateProjectCommand.NotifyCanExecuteChanged();
                _compatibilityWarningDismissed = false;
                UpdateCompatibilityWarning();
                RefreshUnderlyingMap();
            }
            else if (args.PropertyName is nameof(EditorSession.FoundryMapsPath))
            {
                RefreshUnderlyingMap();
            }
            else if (args.PropertyName is nameof(EditorSession.ProjectFilePath))
            {
                OnPropertyChanged(nameof(IsProjectOpen));
                UpdateProjectCommand.NotifyCanExecuteChanged();
            }
        };
    }

    [RelayCommand]
    private void OpenImportSettings() => ShowSettings(SettingsCategory.Import);

    [RelayCommand]
    private void OpenExportSettings() => ShowSettings(SettingsCategory.Export);

    private void ShowSettings(SettingsCategory category)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow is null)
        {
            return;
        }

        SettingsWindow.Show(desktop.MainWindow, _session, category);
    }

    [RelayCommand(CanExecute = nameof(CanReportCompatibilityIssue))]
    private async Task ReportCompatibilityIssueAsync()
    {
        var mapName = _session.SourceFileName ?? _session.Map?.Name ?? "map";
        await ReportIssueService.ReportCompatibilityIssueAsync(
            _session,
            $"Unknown commands in {mapName}").ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanDismissCompatibilityWarning))]
    private void DismissCompatibilityWarning()
    {
        _compatibilityWarningDismissed = true;
        SetCompatibilityWarningVisible(false);
        DismissCompatibilityWarningCommand.NotifyCanExecuteChanged();
    }

    private void UpdateCompatibilityWarning()
    {
        var report = _session.Map?.Compatibility;
        var hasUnknown = report?.UnknownCount > 0;
        CompatibilityWarningMessage = hasUnknown
            ? $"{report!.UnknownCount} unknown command(s) found during import. Things are probably fine, but it would be great if you reported this on GitHub."
            : string.Empty;
        SetCompatibilityWarningVisible(hasUnknown && !_compatibilityWarningDismissed);
        ReportCompatibilityIssueCommand.NotifyCanExecuteChanged();
        DismissCompatibilityWarningCommand.NotifyCanExecuteChanged();
    }

    private void SetCompatibilityWarningVisible(bool visible)
    {
        if (ShowCompatibilityWarning == visible)
        {
            return;
        }

        ShowCompatibilityWarning = visible;
        FileDockExpandedChanged?.Invoke(visible);
    }

    private bool CanReportCompatibilityIssue() =>
        !_compatibilityWarningDismissed && _session.Map?.Compatibility?.UnknownCount > 0;

    private bool CanDismissCompatibilityWarning() => ShowCompatibilityWarning;

    /// <summary>True while a <c>.brickwork</c> project is open (shows Update Project).</summary>
    public bool IsProjectOpen => _session.ProjectFilePath is not null;

    [RelayCommand]
    private async Task OpenSourceMapAsync()
    {
        var path = await PickSourceMapAsync().ConfigureAwait(true);
        if (path is null)
        {
            return;
        }

        if (ProjectFiles.IsProjectPath(path))
        {
            await OpenProjectAsync(path).ConfigureAwait(true);
            return;
        }

        try
        {
            await using var input = File.OpenRead(path);
            var importer = ServiceFactory.CreateImporterForPath(path, _session.CreateImportOptions());
            var map = await importer.ImportAsync(input).ConfigureAwait(true);
            MapReimportMerger.RecordImportedIds(map);
            map.SourceFileName = Path.GetFileName(path);
            _session.SourceFilePath = path;
            _session.ProjectFilePath = null;
            _session.Map = map;
            _session.SourceFileName = Path.GetFileName(path);
            StatusMessage = $"Loaded {_session.SourceFileName}.";
            UpdateCompatibilityWarning();
            RefreshUnderlyingMap();
        }
        catch (Exception ex)
        {
            _session.Map = null;
            _session.SourceFileName = null;
            _session.SourceFilePath = null;
            _session.ProjectFilePath = null;
            StatusMessage = $"Failed to load input: {ex.Message}";
            UpdateCompatibilityWarning();
            RefreshUnderlyingMap();
        }
    }

    private async Task OpenProjectAsync(string path)
    {
        try
        {
            await ProjectFiles.OpenAsync(_session, path).ConfigureAwait(true);
            StatusMessage = $"Opened project {Path.GetFileName(path)}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to open project: {ex.Message}";
        }

        UpdateCompatibilityWarning();
        RefreshUnderlyingMap();
    }

    [RelayCommand(CanExecute = nameof(CanSaveProject))]
    private async Task SaveProjectAsync()
    {
        if (_session.ProjectFilePath is { } path)
        {
            await SaveProjectToAsync(path).ConfigureAwait(true);
        }
        else
        {
            await SaveProjectAsAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveProject))]
    private async Task SaveProjectAsAsync()
    {
        var suggestedName = Path.GetFileNameWithoutExtension(_session.ProjectFilePath ?? _session.SourceFileName ?? "map");
        var path = await PickProjectDestinationAsync(suggestedName).ConfigureAwait(true);
        if (path is null)
        {
            StatusMessage = "Save cancelled.";
            return;
        }

        await SaveProjectToAsync(path).ConfigureAwait(true);
    }

    private async Task SaveProjectToAsync(string path)
    {
        try
        {
            await ProjectFiles.SaveAsync(_session, path).ConfigureAwait(true);
            StatusMessage = $"Saved project {Path.GetFileName(path)}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not save the project: {ex.Message}";
        }
    }

    private bool CanSaveProject() => _session.Map is not null;

    [RelayCommand(CanExecute = nameof(CanUpdateProject))]
    private async Task UpdateProjectAsync()
    {
        var path = await PickSourceMapAsync("Choose a newer backup of the map", includeProjects: false).ConfigureAwait(true);
        if (path is null)
        {
            return;
        }

        try
        {
            StatusMessage = await ProjectFiles.UpdateAsync(_session, path).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not update the project: {ex.Message}";
        }

        UpdateCompatibilityWarning();
        RefreshUnderlyingMap();
    }

    private bool CanUpdateProject() => _session.Map is not null && _session.ProjectFilePath is not null;

    private void RefreshUnderlyingMap()
    {
        if (_session.Map is null)
        {
            UnderlyingMapTooltip = string.Empty;
            ShowUnderlyingMapStatus = false;
            return;
        }

        var sourceFileName = _session.Map.SourceFileName ?? _session.SourceFileName;
        var underlying = UnderlyingMapResolver.TryResolve(
            _session.FoundryMapsPath,
            sourceFileName,
            _session.Map.Name);
        _session.Map.UnderlyingMap = underlying;

        if (underlying is { IsConnected: true })
        {
            UnderlyingMapLinkIconKind = PackIconFontAwesome6Kind.LinkSolid;
            UnderlyingMapLinkBrush = UnderlyingMapFoundBrush;
            UnderlyingMapTooltip =
                $"Found map image {underlying.FileName} with dimensions {underlying.Width}×{underlying.Height}";
        }
        else
        {
            UnderlyingMapLinkIconKind = PackIconFontAwesome6Kind.LinkSlashSolid;
            UnderlyingMapLinkBrush = UnderlyingMapMissingBrush;
            UnderlyingMapTooltip = "No map image found in maps folder. You will have to manually select the map image again in FoundryVTT. You can change the maps folder in the settings.";
        }

        ShowUnderlyingMapStatus = true;
    }

    [RelayCommand(CanExecute = nameof(CanConvert))]
    private async Task ConvertAsync()
    {
        if (_session.Map is null)
        {
            StatusMessage = "Open a source map first.";
            return;
        }

        var suggestedName = _session.SourceFileName is null
            ? "map"
            : Path.GetFileNameWithoutExtension(_session.SourceFileName);

        var exportTarget = await PickExportDestinationAsync(suggestedName).ConfigureAwait(true);
        if (exportTarget is null)
        {
            StatusMessage = "Export cancelled.";
            return;
        }

        var (outputPath, formatId) = exportTarget.Value;

        if (formatId == "foundry" && _session.Map.Grid is { Columns: > 0, Rows: > 0 } mapGrid &&
            Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
        {
            // Start from the last export of this map, else the map grid with the configured pixels.
            var initial = _session.Map.FoundryExportGrid is { IsValid: true } previous
                ? previous
                : new FoundryExportGrid(mapGrid.Columns, mapGrid.Rows, _session.FoundryGridSize);
            var grid = await FoundryExportDialog.ShowAsync(owner, mapGrid, initial).ConfigureAwait(true);
            if (grid is null)
            {
                StatusMessage = "Export cancelled.";
                return;
            }

            _session.Map.FoundryExportGrid = grid;
        }

        StatusMessage = "Converting...";

        try
        {
            _session.Map!.SourceFileName ??= _session.SourceFileName;
            _session.Map.FoundryMapsDirectory = string.IsNullOrWhiteSpace(_session.FoundryMapsPath)
                ? null
                : _session.FoundryMapsPath;
            _session.Map.FoundryGridSize = _session.FoundryGridSize;
            _session.Map.SceneLevelCount = _session.SceneLevelCount;
            _session.Map.GlobalIllumination = _session.GlobalIllumination;

            await using var output = File.Create(outputPath);
            await _convertMapService
                .ConvertAsync(_session.Map, output, formatId)
                .ConfigureAwait(true);

            StatusMessage = $"Exported to {Path.GetFileName(outputPath)} ({formatId}).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Conversion failed: {ex.Message}";
        }
    }

    private bool CanConvert() => _session.Map is not null;

    private async Task<string?> PickSourceMapAsync(string title = "Open map or project", bool includeProjects = true)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            return null;
        }

        var window = desktop.MainWindow;
        if (window?.StorageProvider is not { } storageProvider)
        {
            return null;
        }

        string[] mapPatterns = ["*.ink", "*.uvtt", "*.dd2vtt", "*.df2vtt", "*.json"];
        string[] projectPatterns = [$"*{BrickworkProject.Extension}"];
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("All supported files")
                {
                    Patterns = includeProjects ? [.. projectPatterns, .. mapPatterns] : mapPatterns,
                },
                .. includeProjects
                    ? new[] { new FilePickerFileType("Brickwork projects") { Patterns = projectPatterns } }
                    : [],
                new FilePickerFileType("Inkarnate maps") { Patterns = ["*.ink"] },
                new FilePickerFileType("Universal VTT") { Patterns = ["*.uvtt", "*.dd2vtt", "*.df2vtt"] },
                new FilePickerFileType("JSON files") { Patterns = ["*.json"] },
                new FilePickerFileType("All files") { Patterns = ["*.*"] },
            ],
        }).ConfigureAwait(true);

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    private static async Task<string?> PickProjectDestinationAsync(string suggestedBaseName)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.StorageProvider is not { } storageProvider)
        {
            return null;
        }

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save project",
            FileTypeChoices = [new FilePickerFileType("Brickwork project") { Patterns = [$"*{BrickworkProject.Extension}"] }],
            DefaultExtension = BrickworkProject.Extension.TrimStart('.'),
            // Without the extension: the macOS save panel adds DefaultExtension to the name again.
            SuggestedFileName = suggestedBaseName,
        }).ConfigureAwait(true);

        return file?.Path.LocalPath;
    }

    private async Task<(string Path, string FormatId)?> PickExportDestinationAsync(string suggestedBaseName)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            return null;
        }

        var window = desktop.MainWindow;
        if (window?.StorageProvider is not { } storageProvider)
        {
            return null;
        }

        var defaultChoice = _exportFormatChoices[0];
        var fileTypes = _exportFormatChoices.Select(choice => choice.ToFileType()).ToList();

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export to VTT",
            FileTypeChoices = fileTypes,
            DefaultExtension = defaultChoice.Extension.TrimStart('.'),
            SuggestedFileName = $"{suggestedBaseName}{defaultChoice.Extension}",
        }).ConfigureAwait(true);

        if (file is null)
        {
            return null;
        }

        var outputPath = file.Path.LocalPath;
        var extension = Path.GetExtension(outputPath);
        var formatId = _exportFormatChoices
            .FirstOrDefault(choice => string.Equals(choice.Extension, extension, StringComparison.OrdinalIgnoreCase))
            ?.FormatId ?? defaultChoice.FormatId;

        return (outputPath, formatId);
    }
}
