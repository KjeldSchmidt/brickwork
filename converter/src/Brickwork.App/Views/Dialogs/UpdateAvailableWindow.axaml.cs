using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Brickwork.App.Views.Dialogs;

public partial class UpdateAvailableWindow : Window
{
    private readonly UpdateCheckResult _result;
    private readonly UpdateCheckService _service;

    public UpdateAvailableWindow()
        : this(
            new UpdateCheckResult { CurrentVersion = "unknown", UpdateAvailable = true },
            new UpdateCheckService())
    {
    }

    public UpdateAvailableWindow(UpdateCheckResult result, UpdateCheckService service)
    {
        _result = result;
        _service = service;
        InitializeComponent();
        MessageText.Text =
            $"Brickwork {_result.LatestVersion} is available (you have {_result.CurrentVersion}).";

        if (!string.IsNullOrWhiteSpace(_result.ReleaseNotes))
        {
            NotesHeading.IsVisible = true;
            NotesBorder.IsVisible = true;
            NotesText.Text = _result.ReleaseNotes;
        }
    }

    public static void Show(Window owner, UpdateCheckResult result, UpdateCheckService? service = null)
    {
        var dialog = new UpdateAvailableWindow(result, service ?? new UpdateCheckService());
        dialog.Show(owner);
    }

    private void OnLaterClick(object? sender, RoutedEventArgs e) => Close();

    private void OnSkipClick(object? sender, RoutedEventArgs e)
    {
        _service.SkipRelease(_result.LatestVersion);
        Close();
    }

    private void OnDownloadClick(object? sender, RoutedEventArgs e)
    {
        var url = string.IsNullOrWhiteSpace(_result.ReleaseUrl)
            ? GitHubIssueReporter.ReleasesUrl
            : _result.ReleaseUrl;
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true,
        });
        Close();
    }
}
