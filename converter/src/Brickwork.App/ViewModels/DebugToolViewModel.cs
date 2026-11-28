using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Brickwork.Core.Models;

namespace Brickwork.App.ViewModels;

public partial class DebugToolViewModel : Tool
{
    private readonly EditorSession _session;

    [ObservableProperty]
    private string _compatibilityMessage = "Open a source map to inspect compatibility and JSON.";

    [ObservableProperty]
    private string _compatibilityUnknownActionsMessage = string.Empty;

    [ObservableProperty]
    private string _compatibilityTransactionsMessage = string.Empty;

    [ObservableProperty]
    private bool _hasCompatibilityDetails;

    [ObservableProperty]
    private bool _hasUnknownActions;

    [ObservableProperty]
    private bool _hasSourceJson;

    [ObservableProperty]
    private string _sourceJsonSummary = "No source file loaded.";

    [ObservableProperty]
    private string _sourceJsonStatus = string.Empty;

    [ObservableProperty]
    private string _selectedEntitySummary = "Click a wall, region, or light to inspect it.";

    public DebugToolViewModel(EditorSession session)
    {
        _session = session;
        _session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(EditorSession.Map) or nameof(EditorSession.SourceFilePath))
            {
                RefreshFromSession();
            }

            if (args.PropertyName is nameof(EditorSession.FocusedWallEntityId)
                or nameof(EditorSession.FocusedPortal)
                or nameof(EditorSession.FocusedRegionEntityId)
                or nameof(EditorSession.FocusedLightEntityId)
                or nameof(EditorSession.ContentRevision))
            {
                if (args.PropertyName is nameof(EditorSession.ContentRevision) && _session.IsGestureActive)
                {
                    return;
                }

                RefreshSelectedEntity();
            }
        };
        RefreshFromSession();
        RefreshSelectedEntity();
    }

    [RelayCommand(CanExecute = nameof(CanReportCompatibilityIssue))]
    private async Task ReportCompatibilityIssueAsync()
    {
        var mapName = _session.SourceFileName ?? _session.Map?.Name ?? "map";
        await ReportIssueService.ReportCompatibilityIssueAsync(
            _session,
            $"Unknown commands in {mapName}").ConfigureAwait(true);
    }

    private bool CanReportCompatibilityIssue() => _session.Map?.Compatibility?.UnknownCount > 0;

    [RelayCommand(CanExecute = nameof(CanUseSourceJson))]
    private async Task CopySourceJsonAsync()
    {
        var path = _session.SourceFilePath;
        if (path is null || !File.Exists(path))
        {
            SourceJsonStatus = "Source file is no longer available.";
            return;
        }

        SourceJsonStatus = "Reading source JSON...";
        try
        {
            var json = await SourceJsonReader.ReadPrettyAsync(path).ConfigureAwait(true);
            var clipboard = GetClipboard();
            if (clipboard is null)
            {
                SourceJsonStatus = "Clipboard is unavailable.";
                return;
            }

            await clipboard.SetTextAsync(json).ConfigureAwait(true);
            SourceJsonStatus = $"Copied {FormatByteCount(json.Length)} to clipboard.";
        }
        catch (Exception ex)
        {
            SourceJsonStatus = $"Copy failed: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanUseSourceJson))]
    private async Task SaveSourceJsonAsync()
    {
        var path = _session.SourceFilePath;
        if (path is null || !File.Exists(path))
        {
            SourceJsonStatus = "Source file is no longer available.";
            return;
        }

        var suggestedName = Path.GetFileNameWithoutExtension(path) + ".json";
        var outputPath = await PickSavePathAsync(suggestedName).ConfigureAwait(true);
        if (outputPath is null)
        {
            SourceJsonStatus = "Save cancelled.";
            return;
        }

        SourceJsonStatus = "Writing source JSON...";
        try
        {
            var json = await SourceJsonReader.ReadPrettyAsync(path).ConfigureAwait(true);
            await File.WriteAllTextAsync(outputPath, json).ConfigureAwait(true);
            SourceJsonStatus = $"Saved {FormatByteCount(json.Length)} to {Path.GetFileName(outputPath)}.";
        }
        catch (Exception ex)
        {
            SourceJsonStatus = $"Save failed: {ex.Message}";
        }
    }

    private bool CanUseSourceJson() => HasSourceJson;

    private void RefreshSelectedEntity()
    {
        if (_session.Map is null)
        {
            SelectedEntitySummary = "Open a source map to inspect walls, regions, and lights.";
            return;
        }

        if (_session.FocusedWallEntityId is int wallId)
        {
            var wall = _session.Map.Walls.FirstOrDefault(candidate => candidate.EntityId == wallId);
            SelectedEntitySummary = wall is null
                ? $"Wall {wallId} is no longer in the document."
                : WallDebugFormatter.Format(wall, _session.FocusedPortal);
            return;
        }

        if (_session.FocusedRegionEntityId is int regionId)
        {
            var region = _session.Map.Regions.FirstOrDefault(candidate => candidate.EntityId == regionId);
            SelectedEntitySummary = region is null
                ? $"Region {regionId} is no longer in the document."
                : RegionDebugFormatter.Format(region);
            return;
        }

        if (_session.FocusedLightEntityId is int lightId)
        {
            var light = _session.Map.Lights.FirstOrDefault(candidate => candidate.EntityId == lightId);
            SelectedEntitySummary = light is null
                ? $"Light {lightId} is no longer in the document."
                : LightDebugFormatter.Format(light);
            return;
        }

        SelectedEntitySummary = "Click a wall, region, or light to inspect it.";
    }

    private void RefreshFromSession()
    {
        UpdateCompatibilityDisplay(_session.Map?.Compatibility);
        RefreshSelectedEntity();

        var path = _session.SourceFilePath;
        HasSourceJson = !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        CopySourceJsonCommand.NotifyCanExecuteChanged();
        SaveSourceJsonCommand.NotifyCanExecuteChanged();
        ReportCompatibilityIssueCommand.NotifyCanExecuteChanged();

        if (!HasSourceJson)
        {
            SourceJsonSummary = "No source file loaded.";
            SourceJsonStatus = string.Empty;
            return;
        }

        var info = new FileInfo(path!);
        SourceJsonSummary = $"{Path.GetFileName(path)} · {FormatByteCount(info.Length)} on disk";
        SourceJsonStatus = string.Empty;
    }

    private void ClearCompatibilityDisplay()
    {
        CompatibilityMessage = "Open a source map to inspect compatibility and JSON.";
        CompatibilityUnknownActionsMessage = string.Empty;
        CompatibilityTransactionsMessage = string.Empty;
        HasCompatibilityDetails = false;
        HasUnknownActions = false;
        ReportCompatibilityIssueCommand.NotifyCanExecuteChanged();
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
        ReportCompatibilityIssueCommand.NotifyCanExecuteChanged();
    }

    private static Avalonia.Input.Platform.IClipboard? GetClipboard()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return null;
        }

        return desktop.MainWindow?.Clipboard;
    }

    private static async Task<string?> PickSavePathAsync(string suggestedFileName)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return null;
        }

        var window = desktop.MainWindow;
        if (window?.StorageProvider is not { } storageProvider)
        {
            return null;
        }

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save source JSON",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "json",
            FileTypeChoices =
            [
                new FilePickerFileType("JSON files") { Patterns = ["*.json"] },
                new FilePickerFileType("All files") { Patterns = ["*.*"] },
            ],
        }).ConfigureAwait(true);

        return file?.Path.LocalPath;
    }

    private static string FormatByteCount(long bytes)
    {
        const double kib = 1024;
        const double mib = kib * 1024;
        return bytes switch
        {
            >= (long)mib => $"{bytes / mib:0.##} MiB",
            >= (long)kib => $"{bytes / kib:0.##} KiB",
            _ => $"{bytes} B",
        };
    }
}
