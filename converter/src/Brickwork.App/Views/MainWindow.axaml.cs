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
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            viewModel.Session.ClearWallSelection();
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.V:
                viewModel.Session.ActiveMapTool = MapToolKind.Pointer;
                e.Handled = true;
                break;
            case Key.E:
                viewModel.Session.ActiveMapTool = MapToolKind.Eraser;
                e.Handled = true;
                break;
        }
    }
}
