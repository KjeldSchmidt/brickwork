using Avalonia.Controls;
using Avalonia.Interactivity;
using Brickwork.App.ViewModels;

namespace Brickwork.App.Views.Dialogs;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        DataContext = new AboutWindowViewModel();
    }

    public static async Task ShowAsync(Window owner)
    {
        var dialog = new AboutWindow();
        await dialog.ShowDialog(owner).ConfigureAwait(true);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
