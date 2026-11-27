using Avalonia.Controls;
using Avalonia.Interactivity;
using Brickwork.App.ViewModels;

namespace Brickwork.App.Views.Dialogs;

public partial class OpenFoundrySceneWindow : Window
{
    public OpenFoundrySceneWindow()
    {
        InitializeComponent();
    }

    public static async Task<OpenFoundrySceneViewModel?> ShowAsync(Window owner, string? mapsPath)
    {
        var viewModel = new OpenFoundrySceneViewModel(mapsPath);
        var dialog = new OpenFoundrySceneWindow
        {
            DataContext = viewModel,
        };

        var result = await dialog.ShowDialog<bool?>(owner).ConfigureAwait(true);
        return result == true ? viewModel : null;
    }

    private void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not OpenFoundrySceneViewModel vm || !vm.CanOpen)
        {
            return;
        }

        vm.OpenCommand.Execute(null);
        if (vm.Confirmed)
        {
            Close(true);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
