using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Brickwork.App.Views.Dialogs;

namespace Brickwork.App;

public static class ReportIssueService
{
    public static async Task ReportCompatibilityIssueAsync(EditorSession session, string defaultTitle)
    {
        if (GetOwnerWindow() is not { } owner)
        {
            return;
        }

        var viewport = MapScreenshotCapture.FindMainMapViewport();
        var dialog = await ReportIssueDialog.ShowAsync(owner, defaultTitle, viewport).ConfigureAwait(true);

        if (dialog is null)
        {
            return;
        }

        var body = GitHubIssueReporter.BuildCompatibilityIssueBody(
            session.Map,
            session.Map?.Compatibility,
            dialog.ScreenshotPath,
            dialog.IssueTitle);

        GitHubIssueReporter.OpenIssueInBrowser(GitHubIssueReporter.BuildIssueUrl(dialog.IssueTitle, body));
    }

    private static Window? GetOwnerWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }

        return null;
    }
}
