using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Interactivity;
using Brickwork.App.Controls;

namespace Brickwork.App.Views.Dialogs;

public partial class ReportIssueDialog : Window
{
    private MapViewportControl? _viewport;

    public ReportIssueDialog()
    {
        InitializeComponent();
    }

    public string? ScreenshotPath { get; private set; }

    public string IssueTitle { get; private set; } = "Compatibility issue";

    public static async Task<ReportIssueDialog?> ShowAsync(
        Window owner,
        string defaultTitle,
        MapViewportControl? viewport)
    {
        var dialog = new ReportIssueDialog
        {
            Title = "Report issue on GitHub",
            IssueTitle = defaultTitle,
        };
        dialog._viewport = viewport;

        dialog.TitleBox.Text = defaultTitle;
        dialog.ScreenshotButton.IsEnabled = viewport?.Map is not null;

        var result = await dialog.ShowDialog<bool?>(owner);
        return result == true ? dialog : null;
    }

    private async void OnSaveScreenshotClick(object? sender, RoutedEventArgs e)
    {
        if (_viewport is null || ScreenshotPath is not null)
        {
            return;
        }

        var suggestedName = $"brickwork-map-{DateTime.UtcNow:yyyyMMdd-HHmmss}.png";
        var outputPath = await PickSavePathAsync(suggestedName).ConfigureAwait(true);
        if (outputPath is null)
        {
            return;
        }

        try
        {
            var saved = await MapScreenshotCapture.SaveViewportScreenshotAsync(_viewport, outputPath)
                .ConfigureAwait(true);
            if (saved is null)
            {
                return;
            }

            ScreenshotPath = saved;
            ScreenshotButton.Content = "Please attach the saved screenshot to the issue manually";
            ScreenshotButton.IsEnabled = false;
        }
        catch
        {
            // Leave the button as "Save screenshot" so the user can retry.
        }
    }

    private async Task<string?> PickSavePathAsync(string suggestedFileName)
    {
        if (StorageProvider is not { } storageProvider)
        {
            return null;
        }

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save map screenshot",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "png",
            FileTypeChoices =
            [
                new FilePickerFileType("PNG images") { Patterns = ["*.png"] },
                new FilePickerFileType("All files") { Patterns = ["*.*"] },
            ],
        }).ConfigureAwait(true);

        return file?.TryGetLocalPath();
    }

    private void OnSubmitClick(object? sender, RoutedEventArgs e)
    {
        IssueTitle = string.IsNullOrWhiteSpace(TitleBox.Text)
            ? "Compatibility issue"
            : TitleBox.Text.Trim();
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
