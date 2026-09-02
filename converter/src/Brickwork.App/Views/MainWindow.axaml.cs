using Avalonia.Controls;
using Avalonia.Input;
using Brickwork.App.ViewModels;

namespace Brickwork.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.Session.ClearWallSelection();
        e.Handled = true;
    }
}
