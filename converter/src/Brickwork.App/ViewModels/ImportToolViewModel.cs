using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Brickwork.Composition;
using Brickwork.Core.Geometry;
using Brickwork.Core.Ports;
using Brickwork.Core.Services;

namespace Brickwork.App.ViewModels;

public partial class ImportToolViewModel : Tool
{
    private readonly EditorSession _session;
    private readonly ConvertMapService _convertMapService = ServiceFactory.CreateGuiConvertMapService();
    private readonly IMapImporter _importer = ServiceFactory.CreateInkarnateImporter();
    private readonly IReadOnlyList<ExportFormatChoice> _exportFormatChoices;

    [ObservableProperty]
    private string _statusMessage = "Ready.";

    [ObservableProperty]
    private bool _showCompatibilityWarning;

    [ObservableProperty]
    private string _compatibilityWarningMessage = string.Empty;

    public ImportToolViewModel(EditorSession session)
    {
        _session = session;
        _exportFormatChoices = ExportFormatChoice.ForFormats(_convertMapService.SupportedExportFormats);
        _session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(EditorSession.Map))
            {
                ConvertCommand.NotifyCanExecuteChanged();
                UpdateCompatibilityWarning();
            }
        };
    }

    [RelayCommand(CanExecute = nameof(CanReportCompatibilityIssue))]
    private async Task ReportCompatibilityIssueAsync()
    {
        var mapName = _session.SourceFileName ?? _session.Map?.Name ?? "map";
        await ReportIssueService.ReportCompatibilityIssueAsync(
            _session,
            $"Unknown commands in {mapName}").ConfigureAwait(true);
    }

    private void UpdateCompatibilityWarning()
    {
        var report = _session.Map?.Compatibility;
        ShowCompatibilityWarning = report?.UnknownCount > 0;
        CompatibilityWarningMessage = report?.UnknownCount > 0
            ? $"{report.UnknownCount} unknown command(s) found during import. See Debug panel or report an issue."
            : string.Empty;
        ReportCompatibilityIssueCommand.NotifyCanExecuteChanged();
    }

    private bool CanReportCompatibilityIssue() => _session.Map?.Compatibility?.UnknownCount > 0;

    [RelayCommand]
    private async Task OpenSourceMapAsync()
    {
        var path = await PickSourceMapAsync().ConfigureAwait(true);
        if (path is null)
        {
            return;
        }

        try
        {
            await using var input = File.OpenRead(path);
            var map = await _importer.ImportAsync(input).ConfigureAwait(true);
            WallPointSimplifier.ApplyAll(map.Walls, _session.WallSimplificationTolerance);
            map.SourceFileName = Path.GetFileName(path);
            _session.SourceFilePath = path;
            _session.Map = map;
            _session.SourceFileName = Path.GetFileName(path);
            StatusMessage = $"Loaded {_session.SourceFileName}.";
            UpdateCompatibilityWarning();
        }
        catch (Exception ex)
        {
            _session.Map = null;
            _session.SourceFileName = null;
            _session.SourceFilePath = null;
            StatusMessage = $"Failed to load input: {ex.Message}";
            UpdateCompatibilityWarning();
        }
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
        StatusMessage = "Converting...";

        try
        {
            _session.Map!.SourceFileName ??= _session.SourceFileName;

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

    private async Task<string?> PickSourceMapAsync()
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

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open source map",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Inkarnate maps") { Patterns = ["*.ink"] },
                new FilePickerFileType("JSON files") { Patterns = ["*.json"] },
                new FilePickerFileType("All files") { Patterns = ["*.*"] },
            ],
        }).ConfigureAwait(true);

        return files.Count > 0 ? files[0].Path.LocalPath : null;
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
