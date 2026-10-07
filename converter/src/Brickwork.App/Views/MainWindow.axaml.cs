using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Brickwork.App.ViewModels;
using Brickwork.App.Views.Panels;
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
            viewModel.Session.ClearRegionSelection();
            e.Handled = true;
            return;
        }

        if ((e.Key is Key.Enter or Key.Return) && e.KeyModifiers == KeyModifiers.None)
        {
            if (TryFinishDrawing(viewModel))
            {
                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.F2 && e.KeyModifiers == KeyModifiers.None)
        {
            if (TryBeginWallsRename() || TryBeginRegionsRename())
            {
                e.Handled = true;
            }

            return;
        }

        if ((e.Key is Key.Delete or Key.Back) && e.KeyModifiers == KeyModifiers.None)
        {
            if (viewModel.Session.DeleteSelection())
            {
                e.Handled = true;
            }

            return;
        }

        if (IsPrimaryModifier(e.KeyModifiers) && e.Key == Key.Z && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            viewModel.Session.Undo();
            e.Handled = true;
            return;
        }

        if ((IsPrimaryModifier(e.KeyModifiers) && e.Key == Key.Y) ||
            (IsPrimaryModifier(e.KeyModifiers) && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.Z))
        {
            viewModel.Session.Redo();
            e.Handled = true;
            return;
        }

        if (IsPrimaryModifier(e.KeyModifiers) && e.Key == Key.C)
        {
            if (viewModel.Session.CopySelection())
            {
                e.Handled = true;
            }

            return;
        }

        if (IsPrimaryModifier(e.KeyModifiers) && e.Key == Key.X)
        {
            if (viewModel.Session.CutSelection())
            {
                e.Handled = true;
            }

            return;
        }

        if (IsPrimaryModifier(e.KeyModifiers) && e.Key == Key.V)
        {
            if (viewModel.Session.PasteClipboard())
            {
                e.Handled = true;
            }

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
            case Key.R:
                viewModel.Session.ActiveMapTool = MapToolKind.RegionEditing;
                e.Handled = true;
                break;
            case Key.E:
                viewModel.Session.ActiveMapTool = MapToolKind.Eraser;
                e.Handled = true;
                break;
        }
    }

    private bool TryBeginWallsRename() =>
        this.GetVisualDescendants()
            .OfType<WallsToolView>()
            .FirstOrDefault()
            ?.TryBeginRenameFromHotkey()
        == true;

    private bool TryBeginRegionsRename() =>
        this.GetVisualDescendants()
            .OfType<RegionsToolView>()
            .FirstOrDefault()
            ?.TryBeginRenameFromHotkey()
        == true;

    private bool IsTextInputFocused()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        return focused is TextBox or NumericUpDown or ComboBox;
    }

    private static bool IsPrimaryModifier(KeyModifiers modifiers) =>
        modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);

    private static bool TryFinishDrawing(MainWindowViewModel viewModel) =>
        viewModel.Layout is not null && TryFinishDrawingInDockable(viewModel.Layout);

    private static bool TryFinishDrawingInDockable(IDockable dockable)
    {
        if (dockable is MapPreviewDocumentViewModel mapPreview)
        {
            return mapPreview.IsDrawing && mapPreview.TryFinishDrawing();
        }

        if (dockable is IDock { VisibleDockables: { } children })
        {
            foreach (var child in children)
            {
                if (child is not null && TryFinishDrawingInDockable(child))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
