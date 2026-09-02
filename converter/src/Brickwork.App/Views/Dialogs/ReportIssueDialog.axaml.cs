using Avalonia.Controls;
using Avalonia.Interactivity;
using Brickwork.Core.Models;

namespace Brickwork.App.Views.Dialogs;

public partial class ReportIssueDialog : Window
{
    public ReportIssueDialog()
    {
        InitializeComponent();
    }

    public bool IncludeScreenshot { get; private set; }

    public bool IncludeSourceFile { get; private set; }

    public string IssueTitle { get; private set; } = "Compatibility issue";

    public static async Task<ReportIssueDialog?> ShowAsync(
        Window owner,
        string defaultTitle,
        bool canIncludeScreenshot)
    {
        var dialog = new ReportIssueDialog
        {
            Title = "Report issue on GitHub",
            IssueTitle = defaultTitle,
        };

        dialog.TitleBox.Text = defaultTitle;
        dialog.ScreenshotOption.IsEnabled = canIncludeScreenshot;
        dialog.ScreenshotOption.IsChecked = false;
        dialog.SourceFileOption.IsChecked = false;

        var result = await dialog.ShowDialog<bool?>(owner);
        return result == true ? dialog : null;
    }

    private void OnSubmitClick(object? sender, RoutedEventArgs e)
    {
        IssueTitle = string.IsNullOrWhiteSpace(TitleBox.Text)
            ? "Compatibility issue"
            : TitleBox.Text.Trim();
        IncludeScreenshot = ScreenshotOption.IsChecked == true;
        IncludeSourceFile = SourceFileOption.IsChecked == true;
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
