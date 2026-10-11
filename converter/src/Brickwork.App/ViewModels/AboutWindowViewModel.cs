using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Brickwork.App.ViewModels;

public partial class AboutWindowViewModel : ObservableObject
{
    public string AppVersionLabel => $"Brickwork {GitHubIssueReporter.GetAppVersion()}";

    public string RepositoryUrl => GitHubIssueReporter.RepositoryUrl;

    [RelayCommand]
    private void OpenRepository()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = RepositoryUrl,
            UseShellExecute = true,
        });
    }
}
