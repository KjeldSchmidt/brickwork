using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using InkarnateTools.App.Views.Dialogs;
using InkarnateTools.Core.Models;

namespace InkarnateTools.App;

public static class ReportIssueService
{
    public static async Task ReportCompatibilityIssueAsync(EditorSession session, string defaultTitle)
    {
        if (GetOwnerWindow() is not { } owner)
        {
            return;
        }

        var viewport = MapScreenshotCapture.FindMainMapViewport();
        var dialog = await ReportIssueDialog.ShowAsync(
            owner,
            defaultTitle,
            canIncludeScreenshot: viewport?.Map is not null).ConfigureAwait(true);

        if (dialog is null)
        {
            return;
        }

        string? screenshotPath = null;
        if (dialog.IncludeScreenshot && viewport is not null)
        {
            screenshotPath = await MapScreenshotCapture.SaveViewportScreenshotAsync(viewport).ConfigureAwait(true);
        }

        var body = GitHubIssueReporter.BuildCompatibilityIssueBody(
            session.Map,
            session.Map?.Compatibility,
            dialog.IncludeSourceFile,
            screenshotPath);

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
