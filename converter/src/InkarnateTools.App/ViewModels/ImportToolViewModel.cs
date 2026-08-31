using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using InkarnateTools.Composition;
using InkarnateTools.Core.Geometry;
using InkarnateTools.Core.Models;
using InkarnateTools.Core.Ports;
using InkarnateTools.Core.Services;

namespace InkarnateTools.App.ViewModels;

public partial class ImportToolViewModel : Tool
{
    private readonly EditorSession _session;
    private readonly ConvertMapService _convertMapService = ServiceFactory.CreateConvertMapService();
    private readonly IMapImporter _importer = ServiceFactory.CreateInkarnateImporter();
    private readonly IReadOnlyList<ExportFormatChoice> _exportFormatChoices;
    private string? _loadedInputPath;

    [ObservableProperty]
    private string _statusMessage = "Ready.";

    [ObservableProperty]
    private string _compatibilityMessage = string.Empty;

    [ObservableProperty]
    private string _compatibilityUnknownActionsMessage = string.Empty;

    [ObservableProperty]
    private string _compatibilityTransactionsMessage = string.Empty;

    [ObservableProperty]
    private bool _hasCompatibilityDetails;

    [ObservableProperty]
    private bool _hasUnknownActions;

    public ImportToolViewModel(EditorSession session)
    {
        _session = session;
        _exportFormatChoices = ExportFormatChoice.ForFormats(_convertMapService.SupportedExportFormats);
        _session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(EditorSession.Map))
            {
                ConvertCommand.NotifyCanExecuteChanged();
            }
        };
    }

    [RelayCommand]
    private async Task OpenSourceMapAsync()
    {
        var path = await PickSourceMapAsync().ConfigureAwait(true);
        if (path is null)
        {
            return;
        }

        _loadedInputPath = path;

        try
        {
            await using var input = File.OpenRead(path);
            var map = await _importer.ImportAsync(input).ConfigureAwait(true);
            WallPointSimplifier.ApplyAll(map.Walls, _session.WallSimplificationTolerance);
            _session.Map = map;
            _session.SourceFileName = Path.GetFileName(path);
            UpdateCompatibilityDisplay(map.Compatibility);
            StatusMessage = $"Loaded {_session.SourceFileName}.";
        }
        catch (Exception ex)
        {
            _session.Map = null;
            _session.SourceFileName = null;
            _loadedInputPath = null;
            ClearCompatibilityDisplay();
            StatusMessage = $"Failed to load input: {ex.Message}";
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

    private void ClearCompatibilityDisplay()
    {
        CompatibilityMessage = string.Empty;
        CompatibilityUnknownActionsMessage = string.Empty;
        CompatibilityTransactionsMessage = string.Empty;
        HasCompatibilityDetails = false;
        HasUnknownActions = false;
    }

    private void UpdateCompatibilityDisplay(CompatibilityReport? report)
    {
        if (report is null)
        {
            ClearCompatibilityDisplay();
            return;
        }

        CompatibilityMessage = report.FormatSummary().TrimEnd();
        CompatibilityUnknownActionsMessage = report.FormatUnknownActions().TrimEnd();
        CompatibilityTransactionsMessage = report.FormatTransactionLines().TrimEnd();
        HasCompatibilityDetails = report.TotalTransactions > 0;
        HasUnknownActions = report.UnknownCount > 0;
        ConvertCommand.NotifyCanExecuteChanged();
    }

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
            Title = "Export map",
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
