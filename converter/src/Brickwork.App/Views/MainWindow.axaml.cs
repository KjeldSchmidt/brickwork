using Avalonia.Controls;
using Avalonia.Input;
using Brickwork.App.ViewModels;
using Dock.Model.Core;

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

        if (IsTextInputFocused())
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            viewModel.Session.ClearWallSelection();
            e.Handled = true;
            return;
        }

        if ((e.Key is Key.Enter or Key.Return) && e.KeyModifiers == KeyModifiers.None)
        {
            if (TryFinishDrawingWall(viewModel))
            {
                e.Handled = true;
            }

            return;
        }

        if ((e.Key is Key.Delete or Key.Back) && e.KeyModifiers == KeyModifiers.None)
        {
            if (viewModel.Session.DeleteSelectedWalls())
            {
                e.Handled = true;
            }

            return;
        }

        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.Z)
        {
            viewModel.Session.Undo();
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.Y)
        {
            viewModel.Session.Redo();
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.Z)
        {
            viewModel.Session.Redo();
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
                viewModel.Session.ActiveMapTool = MapToolKind.WallEditing;
                e.Handled = true;
                break;
            case Key.E:
                viewModel.Session.ActiveMapTool = MapToolKind.Eraser;
                e.Handled = true;
                break;
        }
    }

    private bool IsTextInputFocused()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        return focused is TextBox or NumericUpDown or ComboBox;
    }

    private static bool TryFinishDrawingWall(MainWindowViewModel viewModel) =>
        viewModel.Layout is not null && TryFinishDrawingWallInDockable(viewModel.Layout);

    private static bool TryFinishDrawingWallInDockable(IDockable dockable)
    {
        if (dockable is MapPreviewDocumentViewModel mapPreview)
        {
            return mapPreview.IsDrawingWall && mapPreview.TryFinishDrawingWall();
        }

        if (dockable is IDock { VisibleDockables: { } children })
        {
            foreach (var child in children)
            {
                if (child is not null && TryFinishDrawingWallInDockable(child))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
