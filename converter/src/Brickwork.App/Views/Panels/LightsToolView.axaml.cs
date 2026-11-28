using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using Brickwork.App.ViewModels;

namespace Brickwork.App.Views.Panels;

public partial class LightsToolView : UserControl
{
    public LightsToolView()
    {
        InitializeComponent();
    }

    private void OnLightPointerEntered(object? sender, PointerEventArgs e)
    {
        if (DataContext is LightsToolViewModel viewModel && sender is Control { DataContext: LightItemViewModel item })
        {
            viewModel.SetHoveredItem(item);
        }
    }

    private void OnTreePointerExited(object? sender, PointerEventArgs e)
    {
        if (DataContext is LightsToolViewModel viewModel)
        {
            viewModel.SetHoveredItem(null);
        }
    }

    private void OnTreePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not LightsToolViewModel viewModel)
        {
            return;
        }

        var point = e.GetCurrentPoint(LightsTree);
        if (!point.Properties.IsLeftButtonPressed || IsEditorInteraction(e.Source))
        {
            return;
        }

        var treeItem = ResolveTreeViewItem(LightsTree, e.GetPosition(LightsTree));
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        switch (treeItem?.DataContext)
        {
            case LightItemViewModel light:
                viewModel.SelectLight(light, shift);
                e.Handled = true;
                break;
            case LightKindViewModel kind:
                viewModel.SelectKind(kind, shift);
                e.Handled = true;
                break;
        }
    }

    private static bool IsEditorInteraction(object? source)
    {
        if (source is CheckBox or ComboBox or TextBox or NumericUpDown or Slider or Button or ToggleButton or Popup or PopupRoot)
        {
            return true;
        }

        return (source as Control)?.GetVisualAncestors().Any(ancestor =>
            ancestor is CheckBox or ComboBox or TextBox or NumericUpDown or Slider or Button or ToggleButton or Popup or PopupRoot) == true;
    }

    private static TreeViewItem? ResolveTreeViewItem(TreeView tree, Point position)
    {
        if (tree.InputHitTest(position) is not Visual visual)
        {
            return null;
        }

        return visual.GetSelfAndVisualAncestors().OfType<TreeViewItem>().FirstOrDefault();
    }
}
