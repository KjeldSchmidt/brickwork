using System.Diagnostics;
using Avalonia.Controls;
using Brickwork.App.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Brickwork.App.ViewModels;

public partial class AboutWindowViewModel : ObservableObject
{
    private readonly UpdateCheckService _updateCheckService = new();

    public string AppVersionLabel => $"Brickwork {GitHubIssueReporter.GetAppVersion()}";

    public string RepositoryUrl => GitHubIssueReporter.RepositoryUrl;

    [ObservableProperty]
    private string _updateStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _isCheckingForUpdates;

    [RelayCommand]
    private void OpenRepository()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = RepositoryUrl,
            UseShellExecute = true,
        });
    }

    [RelayCommand(CanExecute = nameof(CanCheckForUpdates))]
    private async Task CheckForUpdatesAsync(Window? owner)
    {
        IsCheckingForUpdates = true;
        UpdateStatusMessage = "Checking for updates…";
        CheckForUpdatesCommand.NotifyCanExecuteChanged();

        try
        {
            // Manual checks ignore skip floor so the user can still open the dialog.
            var result = await _updateCheckService
                .CheckAsync(respectSkipped: false)
                .ConfigureAwait(true);

            if (result.CheckFailed)
            {
                UpdateStatusMessage = "Could not check for updates. Try again later.";
                return;
            }

            if (!result.UpdateAvailable)
            {
                UpdateStatusMessage = "You're on the latest release.";
                return;
            }

            UpdateStatusMessage = string.Empty;
            if (owner is not null)
            {
                UpdateAvailableWindow.Show(owner, result, _updateCheckService);
            }
        }
        finally
        {
            IsCheckingForUpdates = false;
            CheckForUpdatesCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanCheckForUpdates() => !IsCheckingForUpdates;
}
