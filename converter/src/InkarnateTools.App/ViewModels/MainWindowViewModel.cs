using System.Collections.ObjectModel;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InkarnateTools.Composition;
using InkarnateTools.Core.Services;

namespace InkarnateTools.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ConvertMapService _convertMapService = ServiceFactory.CreateConvertMapService();

    [ObservableProperty]
    private string _inputPath = string.Empty;

    [ObservableProperty]
    private string _outputPath = string.Empty;

    [ObservableProperty]
    private string _selectedExportFormat = "uvtt2";

    [ObservableProperty]
    private string _statusMessage = "Ready.";

    public ObservableCollection<string> ExportFormats { get; }

    public MainWindowViewModel()
    {
        ExportFormats = new ObservableCollection<string>(_convertMapService.SupportedExportFormats);
    }

    [RelayCommand]
    private async Task BrowseInputAsync()
    {
        var path = await PickFileAsync(isSave: false, "JSON files|*.json|All files|*.*").ConfigureAwait(true);
        if (path is not null)
        {
            InputPath = path;
        }
    }

    [RelayCommand]
    private async Task BrowseOutputAsync()
    {
        var path = await PickFileAsync(isSave: true, "All files|*.*").ConfigureAwait(true);
        if (path is not null)
        {
            OutputPath = path;
        }
    }

    [RelayCommand(CanExecute = nameof(CanConvert))]
    private async Task ConvertAsync()
    {
        StatusMessage = "Converting...";

        try
        {
            await using var input = File.OpenRead(InputPath);
            await using var output = File.Create(OutputPath);
            await _convertMapService
                .ConvertAsync(input, output, SelectedExportFormat)
                .ConfigureAwait(true);

            StatusMessage = $"Converted to {SelectedExportFormat}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Conversion failed: {ex.Message}";
        }
    }

    private bool CanConvert() =>
        !string.IsNullOrWhiteSpace(InputPath) &&
        !string.IsNullOrWhiteSpace(OutputPath) &&
        !string.IsNullOrWhiteSpace(SelectedExportFormat) &&
        File.Exists(InputPath);

    partial void OnInputPathChanged(string value) => ConvertCommand.NotifyCanExecuteChanged();

    partial void OnOutputPathChanged(string value) => ConvertCommand.NotifyCanExecuteChanged();

    partial void OnSelectedExportFormatChanged(string value) => ConvertCommand.NotifyCanExecuteChanged();

    private static async Task<string?> PickFileAsync(bool isSave, string filter)
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

        if (isSave)
        {
            var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Select output file",
            }).ConfigureAwait(true);

            return file?.Path.LocalPath;
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Inkarnate export",
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
}
